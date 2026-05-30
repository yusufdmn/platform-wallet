namespace PlatformWallet.SagaOrchestrator.Domain;

public sealed class SagaOptions
{
    public const int DefaultHoldTtlSeconds = 24 * 60 * 60;

    public int HoldTtlSeconds { get; init; } = DefaultHoldTtlSeconds;
}
