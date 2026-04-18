using FluentAssertions;
using Xunit;

namespace PlatformWallet.Ledger.UnitTests;

/// <summary>
/// Placeholder. Once PostingPairBuilder is authored in Ledger.Domain,
/// property-based tests (FsCheck) will verify:
///  - any returned pair has amount_signed summing to zero for the same asset,
///  - mismatched assets / non-zero sums / zero amounts are rejected,
///  - Debit/Credit entry_kind sign conventions hold for every Phase.
/// Enforced by TheMainPlan.md §3.3 invariant and the `ledger-invariant-checker`
/// subagent.
/// </summary>
public class PostingPairBuilderTests
{
    [Fact]
    public void Scaffold_placeholder_passes()
    {
        true.Should().BeTrue();
    }
}
