namespace UberEatsWallet.Application.Abstractions;

/// <summary>Ambient time, injected so domain transitions stay testable and free of static clocks.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
