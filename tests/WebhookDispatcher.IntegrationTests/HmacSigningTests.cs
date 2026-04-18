using FluentAssertions;
using Xunit;

namespace PlatformWallet.WebhookDispatcher.IntegrationTests;

[Trait("Category", "Integration")]
public class HmacSigningTests
{
    [Fact(Skip = "Enabled once HmacSigner + delivery pipeline are authored.")]
    public void XSignature_header_matches_hmac_sha256_of_raw_body_bytes()
    {
        // TODO: deliver a message to a local sink, capture body bytes + X-Signature,
        // recompute HMAC, assert equality. Verify TheMainPlan.md §7 step 20.
        true.Should().BeTrue();
    }

    [Fact(Skip = "Enabled once scheduled redelivery + DLQ are wired.")]
    public void Exhausted_redelivery_inserts_failed_webhook_deliveries_row()
    {
        true.Should().BeTrue();
    }
}
