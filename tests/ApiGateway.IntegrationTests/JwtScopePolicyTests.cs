using FluentAssertions;
using Xunit;

namespace PlatformWallet.ApiGateway.IntegrationTests;

[Trait("Category", "Integration")]
public class JwtScopePolicyTests
{
    [Fact(Skip = "Enabled once YARP routes + Keycloak JWT validation are wired.")]
    public void Missing_LedgerWrite_scope_returns_403_on_POST_transactions()
    {
        // TODO: WebApplicationFactory host, issue JWT with only LedgerRead scope,
        // POST /v1/transactions, assert 403. Verify TheMainPlan.md §4.2 step 3.
        true.Should().BeTrue();
    }

    [Fact(Skip = "Enabled once rate-limit middleware is wired.")]
    public void Rate_limit_breach_returns_429_with_RetryAfter_header()
    {
        true.Should().BeTrue();
    }

    [Fact(Skip = "Enabled once idempotency middleware is wired.")]
    public void Repeat_idempotency_key_returns_cached_response_without_forwarding()
    {
        true.Should().BeTrue();
    }
}
