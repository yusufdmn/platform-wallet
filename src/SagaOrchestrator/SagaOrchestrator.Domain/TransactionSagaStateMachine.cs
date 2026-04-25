using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Commands;
using PlatformWallet.Contracts.Events;

namespace PlatformWallet.SagaOrchestrator.Domain;

public sealed class TransactionSagaStateMachine : MassTransitStateMachine<TransactionSagaState>
{
    private const string MintTransactionType = "Mint";

    // ── States ─────────────────────────────────────���──────────────────────────
    public State Submitted  { get; private set; } = null!;
    public State Processing { get; private set; } = null!;
    public State Completed  { get; private set; } = null!;
    public State Failed     { get; private set; } = null!;

    // ── Events ─────────────────���───────────────────────────────────��──────────
    public Event<TransactionSubmitted> TransactionSubmitted { get; private set; } = null!;
    public Event<FundsMinted>          FundsMinted          { get; private set; } = null!;
    public Event<Fault<MintFunds>>     MintFundsFaulted     { get; private set; } = null!;

    public TransactionSagaStateMachine(ILogger<TransactionSagaStateMachine> logger)
    {
        InstanceState(x => x.CurrentState);

        ConfigureEvents();
        ConfigureTransitions(logger);
    }

    private void ConfigureEvents()
    {
        Event(() => TransactionSubmitted,
            e => e.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => FundsMinted,
            e => e.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => MintFundsFaulted,
            e => e.CorrelateById(ctx => ctx.Message.Message.CorrelationId));
    }

    private void ConfigureTransitions(ILogger<TransactionSagaStateMachine> logger)
    {
        Initially(
            When(TransactionSubmitted,
                ctx => string.Equals(ctx.Message.TransactionType, MintTransactionType, StringComparison.OrdinalIgnoreCase))
                .Then(ctx => InitialiseState(ctx.Saga, ctx.Message))
                .PublishAsync(ctx => ctx.Init<MintFunds>(new MintFunds(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.CreditAccountId,
                    ctx.Saga.Amount,
                    ctx.Saga.Asset)))
                .TransitionTo(Processing));

        During(Processing,
            When(FundsMinted)
                .Then(ctx => Touch(ctx.Saga))
                .PublishAsync(ctx => ctx.Init<TransactionMinted>(
                    new TransactionMinted(ctx.Saga.CorrelationId)))
                .TransitionTo(Completed)
                .Then(ctx => logger.LogInformation(
                    "Saga {CorrelationId}: mint completed", ctx.Saga.CorrelationId)),

            When(MintFundsFaulted)
                .Then(ctx => Fail(ctx.Saga, ctx.Message.Exceptions.FirstOrDefault()?.Message ?? "Unknown fault"))
                .PublishAsync(ctx => ctx.Init<TransactionFailed>(
                    new TransactionFailed(ctx.Saga.CorrelationId, ctx.Saga.FailureReason!)))
                .TransitionTo(Failed)
                .Then(ctx => logger.LogError(
                    "Saga {CorrelationId}: mint failed — {Reason}",
                    ctx.Saga.CorrelationId, ctx.Saga.FailureReason)));

        SetCompletedWhenFinalized();
    }

    // ── Helpers ────────────────────────────────��─────────────────────────────���

    private static void InitialiseState(TransactionSagaState saga, TransactionSubmitted msg)
    {
        saga.TransactionType  = msg.TransactionType;
        saga.DebitAccountId   = msg.DebitAccountId == Guid.Empty ? null : msg.DebitAccountId;
        saga.CreditAccountId  = msg.CreditAccountId;
        saga.Amount           = msg.Amount;
        saga.Asset            = msg.Asset;
        saga.CreatedAt        = DateTimeOffset.UtcNow;
        saga.UpdatedAt        = DateTimeOffset.UtcNow;
    }

    private static void Touch(TransactionSagaState saga) =>
        saga.UpdatedAt = DateTimeOffset.UtcNow;

    private static void Fail(TransactionSagaState saga, string reason)
    {
        saga.FailureReason = reason;
        saga.UpdatedAt     = DateTimeOffset.UtcNow;
    }
}
