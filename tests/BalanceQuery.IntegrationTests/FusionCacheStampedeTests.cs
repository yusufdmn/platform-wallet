using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PlatformWallet.BalanceQuery.Domain;
using ZiggyCreatures.Caching.Fusion;
using Xunit;

namespace PlatformWallet.BalanceQuery.IntegrationTests;

[Trait("Category", "Integration")]
public class FusionCacheStampedeTests
{
    [Fact]
    public async Task Hundred_parallel_requests_trigger_exactly_one_backend_call()
    {
        var callCount = 0;
        var accountId = Guid.NewGuid();
        var expected  = new AccountBalance(accountId, "USD", 100m, 0m);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFusionCache()
            .WithDefaultEntryOptions(o => o.Duration = TimeSpan.FromSeconds(30));

        await using var sp    = services.BuildServiceProvider();
        var cache = sp.GetRequiredService<IFusionCache>();

        var tasks = Enumerable.Range(0, 100).Select(_ =>
            cache.GetOrSetAsync<AccountBalance?>(
                $"balance:{accountId}",
                async ct =>
                {
                    Interlocked.Increment(ref callCount);
                    await Task.Delay(10, ct);
                    return expected;
                })
            .AsTask())
            .ToArray();

        var results = await Task.WhenAll(tasks);

        callCount.Should().Be(1, "FusionCache single-flight must coalesce concurrent requests");
        results.Should().AllSatisfy(r => r.Should().BeEquivalentTo(expected));
    }

    [Fact]
    public async Task FailSafe_returns_last_known_good_when_gRPC_throws()
    {
        var accountId  = Guid.NewGuid();
        var staleValue = new AccountBalance(accountId, "USD", 50m, 0m);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFusionCache()
            .WithDefaultEntryOptions(o =>
            {
                o.Duration                 = TimeSpan.FromMilliseconds(100);
                o.IsFailSafeEnabled        = true;
                o.FailSafeMaxDuration      = TimeSpan.FromMinutes(30);
                o.FailSafeThrottleDuration = TimeSpan.FromMilliseconds(100);
            });

        await using var sp    = services.BuildServiceProvider();
        var cache = sp.GetRequiredService<IFusionCache>();

        // Prime the cache with a good value
        await cache.GetOrSetAsync<AccountBalance?>(
            $"balance:{accountId}",
            _ => Task.FromResult<AccountBalance?>(staleValue));

        // Wait for TTL to expire so the next call must re-fetch
        await Task.Delay(200);

        // Factory throws — fail-safe should return stale value
        var result = await cache.GetOrSetAsync<AccountBalance?>(
            $"balance:{accountId}",
            _ => Task.FromException<AccountBalance?>(new InvalidOperationException("gRPC unavailable")));

        result.Should().BeEquivalentTo(staleValue,
            "fail-safe must return last known good when the factory throws after TTL expiry");
    }
}
