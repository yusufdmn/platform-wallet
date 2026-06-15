using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Commands;
using PlatformWallet.Contracts.Events;

namespace PlatformWallet.SagaOrchestrator.Domain;

public sealed class TransactionSagaStateMachine : MassTransitStateMachine<TransactionSagaState>
{
    private const string TransferTransactionType = "Transfer";

    // ── States ────────────────────────────────────────────────────────────────
    public State Submitted  { get; private set; } = null!;
    public State Processing { get; private set; } = null!;
    public State Held       { get; private set; } = null!;
    public State Completed     { get; private set; } = null!;
    public State Failed        { get; private set; } = null!;
    public State VoidStranded  { get; private set; } = null!;

    // ── Events ────────────────────────────────────────────────────────────────
    public Event<TransactionSubmitted>      TransactionSubmitted     { get; private set; } = null!;
    public Event<FundsHeld>                 FundsHeld                { get; private set; } = null!;
    public Event<CaptureTransferRequested>  CaptureTransferRequested { get; private set; } = null!;
    public Event<VoidRequested>             VoidRequested            { get; private set; } = null!;
    public Event<TransferCaptured>          TransferCaptured         { get; private set; } = null!;
    public Event<HoldVoided>                HoldVoided               { get; private set; } = null!;
    // Domain failure events — business-rule violations (no retry, no DLQ)
    public Event<HoldFailed>                HoldFailed               { get; private set; } = null!;
    public Event<CaptureFailed>             CaptureFailed            { get; private set; } = null!;
    public Event<VoidFailed>                VoidFailed               { get; private set; } = null!;
    // System fault events — infrastructure failures (retry → DLQ → failed_messages)
    public Event<Fault<HoldFunds>>          HoldFundsFaulted         { get; private set; } = null!;
    public Event<Fault<CaptureTransfer>>    CaptureTransferFaulted   { get; private set; } = null!;
    public Event<Fault<VoidHold>>           VoidHoldFaulted          { get; private set; } = null!;

    // ── Schedules ─────────────────────────────────────────────────────────────
    public Schedule<TransactionSagaState, HoldExpired> HoldExpirySchedule { get; private set; } = null!;

    public TransactionSagaStateMachine(
        ILogger<TransactionSagaStateMachine> logger,
        SagaOptions                          options)
    {
        InstanceState(x => x.CurrentState);

        ConfigureEvents();
        ConfigureSchedules(options);
        ConfigureTransitions(logger);
    }

    private void ConfigureSchedules(SagaOptions options)
    {
        Schedule(() => HoldExpirySchedule,
            instance => instance.HoldExpiryTokenId,
            s =>
            {
                s.Delay    = TimeSpan.FromSeconds(options.HoldTtlSeconds);
                s.Received = r => r.CorrelateById(ctx => ctx.Message.CorrelationId);
            });
    }

    private void ConfigureEvents()
    {
        Event(() => TransactionSubmitted,
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

        Event(() => HoldFailed,
            e => e.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => CaptureFailed,
            e => e.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => VoidFailed,
            e => e.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => HoldFundsFaulted,
            e => e.CorrelateById(ctx => ctx.Message.Message.CorrelationId));

        Event(() => CaptureTransferFaulted,
            e => e.CorrelateById(ctx => ctx.Message.Message.CorrelationId));

        Event(() => VoidHoldFaulted,
            e => e.CorrelateById(ctx => ctx.Message.Message.CorrelationId));
    }

    private void ConfigureTransitions(ILogger<TransactionSagaStateMachine> logger)
    {
        // ── Transfer flow (hold first) ──────────────────────────────────────────
        // NOTE: Use `Publish(ctx => new T(...))` typed-factory overload, NOT
        // `PublishAsync(ctx => ctx.Init<T>(...))`. The Init<T> path requires the
        // message type to have a parameterless constructor (records with primary
        // constructors do not). The typed factory bypasses the message initializer.
        Initially(
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
            // Hold success → publish TransactionHeld, schedule TTL auto-void, wait for capture or void request
            When(FundsHeld)
                .Then(ctx => Touch(ctx.Saga))
                .Publish(ctx => new TransactionHeld(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.DebitAccountId!.Value,
                    ctx.Saga.CreditAccountId))
                .Schedule(HoldExpirySchedule, ctx => new HoldExpired(ctx.Saga.CorrelationId))
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
                    ctx.Saga.CorrelationId, ctx.Saga.FailureReason))
                .Finalize(),

            // Hold system fault — infrastructure failure
            When(HoldFundsFaulted)
                .Then(ctx => Fail(ctx.Saga, FirstException(ctx.Message)))
                .Publish(ctx => new TransactionFailed(ctx.Saga.CorrelationId, ctx.Saga.FailureReason!))
                .TransitionTo(Failed)
                .Then(ctx => logger.LogError(
                    "Saga {CorrelationId}: hold system fault — {Reason}",
                    ctx.Saga.CorrelationId, ctx.Saga.FailureReason))
                .Finalize());

        During(Held,
            // Capture requested
            When(CaptureTransferRequested)
                .Unschedule(HoldExpirySchedule)
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
                .Unschedule(HoldExpirySchedule)
                .Then(ctx => Touch(ctx.Saga))
                .Publish(ctx => new VoidHold(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.DebitAccountId!.Value,
                    ctx.Saga.Amount,
                    ctx.Saga.Asset))
                .TransitionTo(Processing),

            // Hold TTL elapsed — auto-void
            When(HoldExpirySchedule.Received)
                .Then(ctx =>
                {
                    Touch(ctx.Saga);
                    logger.LogInformation(
                        "Saga {CorrelationId}: hold TTL expired, auto-voiding",
                        ctx.Saga.CorrelationId);
                })
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
                    "Saga {CorrelationId}: transfer captured", ctx.Saga.CorrelationId))
                .Finalize(),

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

            // VoidHold domain failure → park as stranded; hold postings still live; operator must retry
            When(VoidFailed)
                .Then(ctx => Fail(ctx.Saga, ctx.Message.Reason))
                .Publish(ctx => new TransactionFailed(ctx.Saga.CorrelationId, ctx.Saga.FailureReason!))
                .TransitionTo(VoidStranded)
                .Then(ctx => logger.LogError(
                    "Saga {CorrelationId}: void hold stranded (domain failure) — {Reason}",
                    ctx.Saga.CorrelationId, ctx.Saga.FailureReason)),

            // VoidHold system fault → park as stranded; hold postings still live; operator must retry
            When(VoidHoldFaulted)
                .Then(ctx => Fail(ctx.Saga, FirstException(ctx.Message)))
                .Publish(ctx => new TransactionFailed(ctx.Saga.CorrelationId, ctx.Saga.FailureReason!))
                .TransitionTo(VoidStranded)
                .Then(ctx => logger.LogError(
                    "Saga {CorrelationId}: void hold stranded (system fault) — {Reason}",
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
                            ctx.Saga.CorrelationId))
                        .Finalize(),
                    binder => binder
                        .Publish(ctx => new TransactionVoided(
                            ctx.Saga.CorrelationId,
                            ctx.Saga.DebitAccountId!.Value,
                            ctx.Saga.CreditAccountId))
                        .TransitionTo(Completed)
                        .Then(ctx => logger.LogInformation(
                            "Saga {CorrelationId}: hold voided by user request",
                            ctx.Saga.CorrelationId))
                        .Finalize()));

        // Operator-driven retry from VoidStranded — reuses the existing VoidRequested event.
        // IsCompensating is preserved on the row so the success branch routes correctly.
        During(VoidStranded,
            When(VoidRequested)
                .Then(ctx =>
                {
                    ctx.Saga.VoidAttempts++;
                    Touch(ctx.Saga);
                })
                .Publish(ctx => new VoidHold(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.DebitAccountId!.Value,
                    ctx.Saga.Amount,
                    ctx.Saga.Asset))
                .TransitionTo(Processing)
                .Then(ctx => logger.LogInformation(
                    "Saga {CorrelationId}: void retry requested (attempt {Attempt})",
                    ctx.Saga.CorrelationId, ctx.Saga.VoidAttempts)));

        ConfigureIdempotentIgnores();
        ConfigureUnhandledEventLogging(logger);

        SetCompletedWhenFinalized();
    }

    // Silently drop idempotent re-deliveries of events that have already been processed
    // for the current state. Without these, MassTransit raises NotAcceptedStateMachineException
    // which loops through the retry pipeline before landing in the DLQ — wasted CPU for
    // a duplicate that is, by definition, safe to discard.
    private void ConfigureIdempotentIgnores()
    {
        DuringAny(
            Ignore(TransactionSubmitted));

        During(Processing,
            Ignore(CaptureTransferRequested),
            Ignore(VoidRequested));

        During(Held,
            Ignore(FundsHeld));

        During(Completed,
            Ignore(FundsHeld),
            Ignore(TransferCaptured),
            Ignore(HoldVoided),
            Ignore(CaptureTransferRequested),
            Ignore(VoidRequested));

        During(Failed,
            Ignore(HoldFailed),
            Ignore(CaptureFailed), Ignore(VoidFailed),
            Ignore(HoldFundsFaulted),
            Ignore(CaptureTransferFaulted), Ignore(VoidHoldFaulted));

        During(VoidStranded,
            Ignore(HoldVoided));
    }

    // Anything not explicitly handled or Ignored is logged and dropped, so an
    // unrecoverable state/event mismatch never enters the retry → DLQ loop.
    private void ConfigureUnhandledEventLogging(ILogger<TransactionSagaStateMachine> logger)
    {
        OnUnhandledEvent(context =>
        {
            logger.LogError(
                "Saga {CorrelationId} received unexpected event {Event} in state {State} — discarding",
                context.Saga.CorrelationId,
                context.Event.Name,
                context.Saga.CurrentState);
            return context.Ignore();
        });
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
