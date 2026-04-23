using Dapper;
using FluentAssertions;
using Npgsql;
using PlatformWallet.Ledger.IntegrationTests.Fixtures;
using Xunit;

namespace PlatformWallet.Ledger.IntegrationTests;

[Collection(LedgerIntegrationGroup.Name)]
public class ZeroSumInvariantSweep(LedgerIntegrationFixture fixture)
{
    [Fact]
    public async Task Every_tx_phase_pair_sums_to_zero()
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);

        var violations = await conn.QueryAsync<(Guid TxId, string Phase, decimal Sum)>("""
            SELECT tx_id, phase, SUM(amount_signed) AS sum
            FROM postings
            GROUP BY tx_id, phase
            HAVING SUM(amount_signed) <> 0
            """);

        violations.Should().BeEmpty("every (tx_id, phase) pair must sum to zero");
    }

    [Fact]
    public async Task Every_tx_phase_pair_has_exactly_two_rows()
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);

        var violations = await conn.QueryAsync<(Guid TxId, string Phase, int Count)>("""
            SELECT tx_id, phase, COUNT(*) AS count
            FROM postings
            GROUP BY tx_id, phase
            HAVING COUNT(*) <> 2
            """);

        violations.Should().BeEmpty("every (tx_id, phase) pair must have exactly 2 postings");
    }
}
