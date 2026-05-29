using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using PlatformWallet.Contracts.Commands;
using PlatformWallet.Contracts.Events;
using PlatformWallet.SagaOrchestrator.Domain;
using Xunit;

namespace PlatformWallet.SagaOrchestrator.IntegrationTests;

[Trait("Category", "Integration")]
public class SagaStateMachineTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMassTransitTestHarness(x =>
        {
            x.AddSagaStateMachine<TransactionSagaStateMachine, TransactionSagaState>()
                .InMemoryRepository();
        });

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Happy_path_transitions_Submitted_to_Processing_to_Completed_for_Mint()
    {
        await using var sp = BuildProvider();

        var harness     = sp.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var sagaHarness   = harness.GetSagaStateMachineHarness<TransactionSagaStateMachine, TransactionSagaState>();
            var correlationId = NewId.NextGuid();

            await harness.Bus.Publish(new TransactionSubmitted(
                CorrelationId:   correlationId,
                TransactionType: "Mint",
                DebitAccountId:  Guid.Empty,
                CreditAccountId: Guid.NewGuid(),
                Amount:          100m,
                Asset:           "USD"));

            // Saga should be in Processing after receiving TransactionSubmitted
            (await sagaHarness.Exists(correlationId, machine => machine.Processing,
                TimeSpan.FromSeconds(10)))
                .Should().NotBeNull("saga must transition to Processing after TransactionSubmitted");

            await harness.Bus.Publish(new FundsMinted(correlationId));

            await harness.InactivityTask;

            // Saga finalizes immediately on Completed, so assert via published event
            (await harness.Published.Any<TransactionMinted>(x =>
                x.Context.Message.CorrelationId == correlationId))
                .Should().BeTrue("saga must publish TransactionMinted on completion");
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Capture_failure_triggers_VoidHold_and_lands_in_Failed()
    {
        await using var sp = BuildProvider();

        var harness = sp.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var sagaHarness   = harness.GetSagaStateMachineHarness<TransactionSagaStateMachine, TransactionSagaState>();
            var correlationId = NewId.NextGuid();
            var debitAccount  = Guid.NewGuid();
            var creditAccount = Guid.NewGuid();

            // Start transfer saga
            await harness.Bus.Publish(new TransactionSubmitted(
                CorrelationId:   correlationId,
                TransactionType: "Transfer",
                DebitAccountId:  debitAccount,
                CreditAccountId: creditAccount,
                Amount:          50m,
                Asset:           "USD"));

            (await sagaHarness.Exists(correlationId, machine => machine.Processing,
                TimeSpan.FromSeconds(10)))
                .Should().NotBeNull("saga must be Processing after initial submit");

            // Funds held — saga moves to Held
            await harness.Bus.Publish(new FundsHeld(correlationId));

            (await sagaHarness.Exists(correlationId, machine => machine.Held,
                TimeSpan.FromSeconds(10)))
                .Should().NotBeNull("saga must reach Held after FundsHeld");

            // Capture requested — saga goes back to Processing, sends CaptureTransfer
            await harness.Bus.Publish(new CaptureTransferRequested(correlationId));

            (await sagaHarness.Exists(correlationId, machine => machine.Processing,
                TimeSpan.FromSeconds(10)))
                .Should().NotBeNull("saga must be Processing after CaptureTransferRequested");

            // Simulate CaptureTransfer fault → saga compensates with VoidHold
            await harness.Bus.Publish<Fault<CaptureTransfer>>(new
            {
                CorrelationId    = correlationId,
                Message          = new CaptureTransfer(correlationId, debitAccount, creditAccount, 50m, "USD"),
                Exceptions       = new[] { new { Message = "Insufficient funds" } },
                Timestamp        = DateTimeOffset.UtcNow,
                FaultId          = Guid.NewGuid(),
                FaultedMessageId = Guid.NewGuid(),
                MessageId        = Guid.NewGuid(),
                Host             = new { },
            });

            // brief settle — saga publishes VoidHold and stays in Processing (IsCompensating=true)
            await Task.Delay(500);

            // Now send HoldVoided — saga lands in Failed and is finalized
            await harness.Bus.Publish(new HoldVoided(correlationId));

            await harness.InactivityTask;

            // Saga finalizes immediately on Failed, so assert via published event
            (await harness.Published.Any<TransactionFailed>(x =>
                x.Context.Message.CorrelationId == correlationId))
                .Should().BeTrue("saga must publish TransactionFailed on compensation path");
        }
        finally
        {
            await harness.Stop();
        }
    }
}
