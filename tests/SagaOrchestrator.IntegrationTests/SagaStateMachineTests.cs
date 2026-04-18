using FluentAssertions;
using Xunit;

namespace PlatformWallet.SagaOrchestrator.IntegrationTests;

[Trait("Category", "Integration")]
public class SagaStateMachineTests
{
    [Fact(Skip = "Enabled once TransactionSagaStateMachine is authored in Domain.")]
    public void Happy_path_transitions_Submitted_to_Held_to_Captured()
    {
        // TODO: use MassTransit's ITestHarness + Testcontainers Postgres.
        // Assert state after each event; pessimistic lock + partitioner behavior.
        true.Should().BeTrue();
    }

    [Fact(Skip = "Enabled once compensation transitions are wired.")]
    public void Capture_failure_triggers_VoidHold_and_lands_in_Failed()
    {
        true.Should().BeTrue();
    }
}
