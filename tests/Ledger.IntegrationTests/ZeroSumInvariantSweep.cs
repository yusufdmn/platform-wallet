using FluentAssertions;
using Xunit;

namespace PlatformWallet.Ledger.IntegrationTests;

/// <summary>
/// Scaffold of the integration-test invariant sweep from TheMainPlan.md §7 step 12.
/// After every happy-path, compensation-path, mint, burn and retry integration
/// test runs, this assertion MUST pass against ledger_db:
///
///   SELECT tx_id, phase, SUM(amount_signed)
///   FROM postings GROUP BY tx_id, phase
///   HAVING SUM(amount_signed) &lt;&gt; 0;   -- zero rows expected
///
/// Plus: every (tx_id, phase) pair must have exactly 2 rows.
///
/// Once the Testcontainers Postgres fixture + LedgerDbContext are authored,
/// this test evolves into an <see cref="IAsyncDisposable"/> fixture teardown that
/// runs the sweep across the whole integration test run.
/// Tracked by `ledger-invariant-checker` subagent.
/// </summary>
[Trait("Category", "Integration")]
public class ZeroSumInvariantSweep
{
    [Fact(Skip = "Enabled once Testcontainers Postgres fixture + schema are in place.")]
    public void Every_tx_phase_pair_sums_to_zero_and_has_exactly_two_rows()
    {
        // TODO: open Dapper connection to the test container's ledger_db,
        //       assert both invariant queries return empty / two rows.
        false.Should().BeTrue("placeholder for the real sweep");
    }
}
