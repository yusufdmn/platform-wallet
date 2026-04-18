using FluentAssertions;
using Xunit;

namespace PlatformWallet.EndToEnd.Tests;

/// <summary>
/// End-to-end tests that spin the full docker-compose.yml stack and exercise the
/// verification scenarios from TheMainPlan.md §7. Skipped until the compose file
/// and service implementations exist.
/// </summary>
[Trait("Category", "EndToEnd")]
public class FullSystemFlowTests
{
    [Fact(Skip = "Enabled once docker-compose.yml + all services run.")]
    public void Mint_Hold_Capture_produces_balanced_postings_and_delivered_webhook()
    {
        // TheMainPlan.md §7 steps 1..20
        true.Should().BeTrue();
    }

    [Fact(Skip = "Enabled once fault path + compensation are wired.")]
    public void ForceFailCapture_metadata_triggers_void_hold_and_final_failed_status()
    {
        // TheMainPlan.md §7 step 15
        true.Should().BeTrue();
    }

    [Fact(Skip = "Enabled once zero-sum sweep fixture hooks are in place.")]
    public void After_full_scenario_run_zero_sum_invariant_holds_across_all_assets()
    {
        // TheMainPlan.md §7 step 12 — invariant gate.
        true.Should().BeTrue();
    }
}
