using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Commands;
using PlatformWallet.Contracts.Events;

namespace PlatformWallet.SagaOrchestrator.Domain;

public sealed class TransactionSagaStateMachine : MassTransitStateMachine<TransactionSagaState>
{
    private const string MintTransactionType     = "Mint";
    private const string BurnTransactionType     = "Burn";
    private const string TransferTransactionType = "Transfer";

    // ── States ────────────────────────────────────────────────────────────────
    public State Submitted  { get; private set; } = null!;
    public State Processing { get; private set; } = null!;
    public State Held       { get; private set; } = null!;
    public State Completed  { get; private set; } = null!;
    public State Failed     { get; private set; } = null!;

    // ── Events ────────────────────────────────────────────────────────────────
    public Event<TransactionSubmitted>      TransactionSubmitted     { get; private set; } = null!;
    public Event<FundsMinted>               FundsMinted              { get; private set; } = null!;
    public Event<FundsBurned>               FundsBurned              { get; private set; } = null!;
    public Event<FundsHeld>                 FundsHeld                { get; private set; } = null!;
    public Event<CaptureTransferRequested>  CaptureTransferRequested { get; private set; } = null!;
    public Event<VoidRequested>             VoidRequested            { get; private set; } = null!;
    public Event<TransferCaptured>          TransferCaptured         { get; private set; } = null!;
    public Event<HoldVoided>                HoldVoided               { get; private set; } = null!;
    // Domain failure events — business-rule violations (no retry, no DLQ)
    public Event<MintFailed>                MintFailed               { get; private set; } = null!;
    public Event<BurnFailed>                BurnFailed               { get; private set; } = null!;
    public Event<HoldFailed>                HoldFailed               { get; private set; } = null!;
    public Event<CaptureFailed>             CaptureFailed            { get; private set; } = null!;
    public Event<VoidFailed>                VoidFailed               { get; private set; } = null!;
    // System fault events — infrastructure failures (retry → DLQ → failed_messages)
    public Event<Fault<MintFunds>>          MintFundsFaulted         { get; private set; } = null!;
    public Event<Fault<BurnFunds>>          BurnFundsFaulted         { get; private set; } = null!;
    public Event<Fault<HoldFunds>>          HoldFundsFaulted         { get; private set; } = null!;
    public Event<Fault<CaptureTransfer>>    CaptureTransferFaulted   { get; private set; } = null!;
    public Event<Fault<VoidHold>>           VoidHoldFaulted          { get; private set; } = null!;

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

        Event(() => FundsBurned,
            e => e.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => FundsHeld,
            e => e.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => CaptureTransferRequested,
            e => e.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => VoidRequested,
            e => e.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => TransferCaptured,
            e => e.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => HoldVoided,
            e => e.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => MintFailed,
            e => e.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => BurnFailed,
            e => e.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => HoldFailed,
            e => e.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => CaptureFailed,
            e => e.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => VoidFailed,
            e => e.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => MintFundsFaulted,
            e => e.CorrelateById(ctx => ctx.Message.Message.CorrelationId));

        Event(() => BurnFundsFaulted,
            e => e.CorrelateById(ctx => ctx.Message.Message.CorrelationId));

        Event(() => HoldFundsFaulted,
            e => e.CorrelateById(ctx => ctx.Message.Message.CorrelationId));

        Event(() => CaptureTransferFaulted,
            e => e.CorrelateById(ctx => ctx.Message.Message.CorrelationId));

        Event(() => VoidHoldFaulted,
            e => e.CorrelateById(ctx => ctx.Message.Message.CorrelationId));
    }

    private void ConfigureTransitions(ILogger<TransactionSagaStateMachine> logger)
    {
        // ── Mint flow ─────────────────────────────────────────────────────────
        // NOTE: Use `Publish(ctx => new T(...))` typed-factory overload, NOT
        // `PublishAsync(ctx => ctx.Init<T>(...))`. The Init<T> path requires the
        // message type to have a parameterless constructor (records with primary
        // constructors do not). The typed factory bypasses the message initializer.
        Initially(
            When(TransactionSubmitted,
                ctx => string.Equals(ctx.Message.TransactionType, MintTransactionType, StringComparison.OrdinalIgnoreCase))
                .Then(ctx => InitialiseState(ctx.Saga, ctx.Message))
                .Publish(ctx => new MintFunds(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.CreditAccountId,
                    ctx.Saga.Amount,
                    ctx.Saga.Asset))
                .TransitionTo(Processing),

            // ── Burn flow ─────────────────────────────────────────────────────
            When(TransactionSubmitted,
                ctx => string.Equals(ctx.Message.TransactionType, BurnTransactionType, StringComparison.OrdinalIgnoreCase))
                .Then(ctx => InitialiseState(ctx.Saga, ctx.Message))
                .Publish(ctx => new BurnFunds(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.DebitAccountId!.Value,
                    ctx.Saga.Amount,
                    ctx.Saga.Asset))
                .TransitionTo(Processing),

            // ── Transfer flow (hold first) ────────────────────────────────────
            When(TransactionSubmitted,
                ctx => string.Equals(ctx.Message.TransactionType, TransferTransactionType, StringComparison.OrdinalIgnoreCase))
                .Then(ctx => InitialiseState(ctx.Saga, ctx.Message))
                .Publish(ctx => new HoldFunds(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.DebitAccountId!.Value,
                    ctx.Saga.CreditAccountId,
                    ctx.Saga.Amount,
                    ctx.Saga.Asset))
                .TransitionTo(Processing));

        During(Processing,
            // Mint success
            When(FundsMinted)
                .Then(ctx => Touch(ctx.Saga))
                .Publish(ctx => new TransactionMinted(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.DebitAccountId,
                    ctx.Saga.CreditAccountId))
                .TransitionTo(Completed)
                .Then(ctx => logger.LogInformation(
                    "Saga {CorrelationId}: mint completed", ctx.Saga.CorrelationId)),

            // Mint domain failure — business rule violated (no retry)
            When(MintFailed)
                .Then(ctx => Fail(ctx.Saga, ctx.Message.Reason))
                .Publish(ctx => new TransactionFailed(ctx.Saga.CorrelationId, ctx.Saga.FailureReason!))
                .TransitionTo(Failed)
                .Then(ctx => logger.LogWarning(
                    "Saga {CorrelationId}: mint domain failure — {Reason}",
                    ctx.Saga.CorrelationId, ctx.Saga.FailureReason)),

            // Mint system fault — infrastructure failure (retried → DLQ → failed_messages)
            When(MintFundsFaulted)
                .Then(ctx => Fail(ctx.Saga, FirstException(ctx.Message)))
                .Publish(ctx => new TransactionFailed(ctx.Saga.CorrelationId, ctx.Saga.FailureReason!))
                .TransitionTo(Failed)
                .Then(ctx => logger.LogError(
                    "Saga {CorrelationId}: mint system fault — {Reason}",
                    ctx.Saga.CorrelationId, ctx.Saga.FailureReason)),

            // Burn success
            When(FundsBurned)
                .Then(ctx => Touch(ctx.Saga))
                .Publish(ctx => new TransactionBurned(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.DebitAccountId!.Value,
                    ctx.Saga.CreditAccountId))
                .TransitionTo(Completed)
                .Then(ctx => logger.LogInformation(
                    "Saga {CorrelationId}: burn completed", ctx.Saga.CorrelationId)),

            // Burn domain failure — business rule violated (no retry)
            When(BurnFailed)
                .Then(ctx => Fail(ctx.Saga, ctx.Message.Reason))
                .Publish(ctx => new TransactionFailed(ctx.Saga.CorrelationId, ctx.Saga.FailureReason!))
                .TransitionTo(Failed)
                .Then(ctx => logger.LogWarning(
                    "Saga {CorrelationId}: burn domain failure — {Reason}",
                    ctx.Saga.CorrelationId, ctx.Saga.FailureReason)),

            // Burn system fault — infrastructure failure (retried → DLQ → failed_messages)
            When(BurnFundsFaulted)
                .Then(ctx => Fail(ctx.Saga, FirstException(ctx.Message)))
                .Publish(ctx => new TransactionFailed(ctx.Saga.CorrelationId, ctx.Saga.FailureReason!))
                .TransitionTo(Failed)
                .Then(ctx => logger.LogError(
                    "Saga {CorrelationId}: burn system fault — {Reason}",
                    ctx.Saga.CorrelationId, ctx.Saga.FailureReason)),

            // Hold success → publish TransactionHeld, wait for capture or void request
            When(FundsHeld)
                .Then(ctx => Touch(ctx.Saga))
                .Publish(ctx => new TransactionHeld(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.DebitAccountId!.Value,
                    ctx.Saga.CreditAccountId))
                .TransitionTo(Held)
                .Then(ctx => logger.LogInformation(
                    "Saga {CorrelationId}: funds held", ctx.Saga.CorrelationId)),

            // Hold domain failure — business rule violated (no retry)
            When(HoldFailed)
                .Then(ctx => Fail(ctx.Saga, ctx.Message.Reason))
                .Publish(ctx => new TransactionFailed(ctx.Saga.CorrelationId, ctx.Saga.FailureReason!))
                .TransitionTo(Failed)
                .Then(ctx => logger.LogWarning(
                    "Saga {CorrelationId}: hold domain failure — {Reason}",
                    ctx.Saga.CorrelationId, ctx.Saga.FailureReason)),

            // Hold system fault — infrastructure failure
            When(HoldFundsFaulted)
                .Then(ctx => Fail(ctx.Saga, FirstException(ctx.Message)))
                .Publish(ctx => new TransactionFailed(ctx.Saga.CorrelationId, ctx.Saga.FailureReason!))
                .TransitionTo(Failed)
                .Then(ctx => logger.LogError(
                    "Saga {CorrelationId}: hold system fault — {Reason}",
                    ctx.Saga.CorrelationId, ctx.Saga.FailureReason)));

        During(Held,
            // Capture requested
            When(CaptureTransferRequested)
                .Then(ctx => Touch(ctx.Saga))
                .Publish(ctx => new CaptureTransfer(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.DebitAccountId!.Value,
                    ctx.Saga.CreditAccountId,
                    ctx.Saga.Amount,
                    ctx.Saga.Asset))
                .TransitionTo(Processing),

            // Void requested — compensate
            When(VoidRequested)
                .Then(ctx => Touch(ctx.Saga))
                .Publish(ctx => new VoidHold(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.DebitAccountId!.Value,
                    ctx.Saga.Amount,
                    ctx.Saga.Asset))
                .TransitionTo(Processing));

        During(Processing,
            // Capture success
            When(TransferCaptured)
                .Then(ctx => Touch(ctx.Saga))
                .Publish(ctx => new TransactionCaptured(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.DebitAccountId!.Value,
                    ctx.Saga.CreditAccountId))
                .TransitionTo(Completed)
                .Then(ctx => logger.LogInformation(
                    "Saga {CorrelationId}: transfer captured", ctx.Saga.CorrelationId)),

            // Capture domain failure → void to compensate
            When(CaptureFailed)
                .Then(ctx =>
                {
                    Fail(ctx.Saga, ctx.Message.Reason);
                    ctx.Saga.IsCompensating = true;
                })
                .Publish(ctx => new VoidHold(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.DebitAccountId!.Value,
                    ctx.Saga.Amount,
                    ctx.Saga.Asset))
                .Then(ctx => logger.LogWarning(
                    "Saga {CorrelationId}: capture domain failure, voiding hold — {Reason}",
                    ctx.Saga.CorrelationId, ctx.Saga.FailureReason)),

            // Capture system fault → void to compensate
            When(CaptureTransferFaulted)
                .Then(ctx =>
                {
                    Fail(ctx.Saga, FirstException(ctx.Message));
                    ctx.Saga.IsCompensating = true;
                })
                .Publish(ctx => new VoidHold(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.DebitAccountId!.Value,
                    ctx.Saga.Amount,
                    ctx.Saga.Asset))
                .Then(ctx => logger.LogError(
                    "Saga {CorrelationId}: capture system fault, voiding hold — {Reason}",
                    ctx.Saga.CorrelationId, ctx.Saga.FailureReason)),

            // VoidHold domain failure → failed
            When(VoidFailed)
                .Then(ctx => Fail(ctx.Saga, ctx.Message.Reason))
                .Publish(ctx => new TransactionFailed(ctx.Saga.CorrelationId, ctx.Saga.FailureReason!))
                .TransitionTo(Failed)
                .Then(ctx => logger.LogWarning(
                    "Saga {CorrelationId}: void hold domain failure — {Reason}",
                    ctx.Saga.CorrelationId, ctx.Saga.FailureReason)),

            // VoidHold system fault → compensate failed, mark as failed
            When(VoidHoldFaulted)
                .Then(ctx => Fail(ctx.Saga, FirstException(ctx.Message)))
                .Publish(ctx => new TransactionFailed(ctx.Saga.CorrelationId, ctx.Saga.FailureReason!))
                .TransitionTo(Failed)
                .Then(ctx => logger.LogError(
                    "Saga {CorrelationId}: void hold system fault — {Reason}",
                    ctx.Saga.CorrelationId, ctx.Saga.FailureReason)),

            // Void success — if compensating then fail, else complete
            When(HoldVoided)
                .Then(ctx => Touch(ctx.Saga))
                .IfElse(ctx => ctx.Saga.IsCompensating,
                    binder => binder
                        .Publish(ctx => new TransactionFailed(ctx.Saga.CorrelationId, ctx.Saga.FailureReason!))
                        .TransitionTo(Failed)
                        .Then(ctx => logger.LogError(
                            "Saga {CorrelationId}: capture compensated via void — failed",
                            ctx.Saga.CorrelationId)),
                    binder => binder
                        .Publish(ctx => new TransactionVoided(
                            ctx.Saga.CorrelationId,
                            ctx.Saga.DebitAccountId!.Value,
                            ctx.Saga.CreditAccountId))
                        .TransitionTo(Completed)
                        .Then(ctx => logger.LogInformation(
                            "Saga {CorrelationId}: hold voided by user request",
                            ctx.Saga.CorrelationId))));

        SetCompletedWhenFinalized();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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

    private static string FirstException<T>(Fault<T> fault) where T : class =>
        fault.Exceptions.FirstOrDefault()?.Message ?? "Unknown fault";
}
