using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using PlatformWallet.Ledger.Domain;
using Xunit;

namespace PlatformWallet.Ledger.UnitTests;

public class PostingPairBuilderTests
{
    private static readonly Guid TxId      = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();

    // ── BuildMint ────────────────────────────────────────────────────────────

    [Property]
    public Property BuildMint_pair_always_sums_to_zero(PositiveInt raw)
    {
        var amount = (decimal)raw.Get / 100m;
        var (debit, credit) = PostingPairBuilder.BuildMint(TxId, AccountId, amount, "USD");
        return (debit.AmountSigned + credit.AmountSigned == 0m).ToProperty();
    }

    [Fact]
    public void BuildMint_debit_targets_world_account()
    {
        var (debit, _) = PostingPairBuilder.BuildMint(TxId, AccountId, 100m, "USD");
        debit.AccountId.Should().Be(SystemAccounts.WorldId);
        debit.EntryKind.Should().Be(EntryKind.Debit);
        debit.AmountSigned.Should().BeNegative();
        debit.Phase.Should().Be(Phase.Mint);
    }

    [Fact]
    public void BuildMint_credit_targets_supplied_account()
    {
        var (_, credit) = PostingPairBuilder.BuildMint(TxId, AccountId, 100m, "USD");
        credit.AccountId.Should().Be(AccountId);
        credit.EntryKind.Should().Be(EntryKind.Credit);
        credit.AmountSigned.Should().BePositive();
        credit.Phase.Should().Be(Phase.Mint);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void BuildMint_throws_for_non_positive_amount(double amount)
    {
        var act = () => PostingPairBuilder.BuildMint(TxId, AccountId, (decimal)amount, "USD");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ── BuildBurn ────────────────────────────────────────────────────────────

    [Property]
    public Property BuildBurn_pair_always_sums_to_zero(PositiveInt raw)
    {
        var amount = (decimal)raw.Get / 100m;
        var (debit, credit) = PostingPairBuilder.BuildBurn(TxId, AccountId, amount, "USD");
        return (debit.AmountSigned + credit.AmountSigned == 0m).ToProperty();
    }

    [Fact]
    public void BuildBurn_credit_targets_world_account()
    {
        var (_, credit) = PostingPairBuilder.BuildBurn(TxId, AccountId, 50m, "USD");
        credit.AccountId.Should().Be(SystemAccounts.WorldId);
        credit.Phase.Should().Be(Phase.Burn);
    }

    // ── BuildHold ────────────────────────────────────────────────────────────

    [Property]
    public Property BuildHold_pair_always_sums_to_zero(PositiveInt raw)
    {
        var amount = (decimal)raw.Get / 100m;
        var (debit, credit) = PostingPairBuilder.BuildHold(TxId, AccountId, amount, "USD");
        return (debit.AmountSigned + credit.AmountSigned == 0m).ToProperty();
    }

    [Fact]
    public void BuildHold_credit_targets_held_pool()
    {
        var (_, credit) = PostingPairBuilder.BuildHold(TxId, AccountId, 25m, "USD");
        credit.AccountId.Should().Be(SystemAccounts.HeldPoolId);
        credit.Phase.Should().Be(Phase.Hold);
    }

    // ── BuildVoid ────────────────────────────────────────────────────────────

    [Property]
    public Property BuildVoid_pair_always_sums_to_zero(PositiveInt raw)
    {
        var amount = (decimal)raw.Get / 100m;
        var (debit, credit) = PostingPairBuilder.BuildVoid(TxId, AccountId, amount, "USD");
        return (debit.AmountSigned + credit.AmountSigned == 0m).ToProperty();
    }

    // ── BuildCapture ─────────────────────────────────────────────────────────

    [Property]
    public Property BuildCapture_pair_always_sums_to_zero(PositiveInt raw)
    {
        var amount     = (decimal)raw.Get / 100m;
        var creditorId = Guid.NewGuid();
        var (debit, credit) = PostingPairBuilder.BuildCapture(TxId, AccountId, creditorId, amount, "USD");
        return (debit.AmountSigned + credit.AmountSigned == 0m).ToProperty();
    }
}
