using FluentAssertions;
using Xunit;

namespace PlatformWallet.BalanceQuery.IntegrationTests;

[Trait("Category", "Integration")]
public class FusionCacheStampedeTests
{
    [Fact(Skip = "Enabled once FusionCache + gRPC client are wired.")]
    public void Hundred_parallel_requests_trigger_exactly_one_backend_call()
    {
        // TODO: stub gRPC client that counts calls. Fire 100 parallel
        // GetBalance requests past cache TTL. Assert count == 1.
        true.Should().BeTrue();
    }

    [Fact(Skip = "Enabled once fail-safe is wired.")]
    public void FailSafe_returns_last_known_good_when_gRPC_throws()
    {
        true.Should().BeTrue();
    }
}
