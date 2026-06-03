using UberEatsWallet.Application.Abstractions;

namespace UberEatsWallet.Infrastructure.Time;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
