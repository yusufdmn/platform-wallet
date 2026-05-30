using System.Diagnostics;
using PlatformWallet.Ledger.Domain.Exceptions;

namespace PlatformWallet.Ledger.Domain;

// The only code path that constructs Posting rows. Every method returns exactly
// two postings whose AmountSigned values sum to zero for the same asset.
public static class PostingPairBuilder
{
    public static (Posting Debit, Posting Credit) BuildMint(
        Guid txId, Guid creditAccountId, decimal amount, string asset)
    {
        EnsurePositive(amount);

        var debit  = Posting.Create(txId, SystemAccounts.WorldId, asset, -amount, EntryKind.Debit,  Phase.Mint);
        var credit = Posting.Create(txId, creditAccountId,        asset, +amount, EntryKind.Credit, Phase.Mint);

        Debug.Assert(debit.AmountSigned + credit.AmountSigned == 0);
        return (debit, credit);
    }

    public static (Posting Debit, Posting Credit) BuildBurn(
        Guid txId, Guid debitAccountId, decimal amount, string asset)
    {
        EnsurePositive(amount);

        var debit  = Posting.Create(txId, debitAccountId,         asset, -amount, EntryKind.Debit,  Phase.Burn);
        var credit = Posting.Create(txId, SystemAccounts.WorldId, asset, +amount, EntryKind.Credit, Phase.Burn);

        Debug.Assert(debit.AmountSigned + credit.AmountSigned == 0);
        return (debit, credit);
    }

    public static (Posting Debit, Posting Credit) BuildHold(
        Guid txId, Guid debitAccountId, decimal amount, string asset)
    {
        EnsurePositive(amount);

        var debit  = Posting.Create(txId, debitAccountId,           asset, -amount, EntryKind.Debit,  Phase.Hold);
        var credit = Posting.Create(txId, SystemAccounts.HeldPoolId, asset, +amount, EntryKind.Credit, Phase.Hold);

        Debug.Assert(debit.AmountSigned + credit.AmountSigned == 0);
        return (debit, credit);
    }

    public static (Posting Debit, Posting Credit) BuildCapture(
        Guid txId, Guid debitAccountId, Guid creditAccountId, decimal amount, string asset)
    {
        EnsurePositive(amount);

        var debit  = Posting.Create(txId, debitAccountId,  asset, -amount, EntryKind.Debit,  Phase.Capture);
        var credit = Posting.Create(txId, creditAccountId, asset, +amount, EntryKind.Credit, Phase.Capture);

        Debug.Assert(debit.AmountSigned + credit.AmountSigned == 0);
        return (debit, credit);
    }

    public static (Posting Debit, Posting Credit) BuildVoid(
        Guid txId, Guid debitAccountId, decimal amount, string asset)
    {
        EnsurePositive(amount);

        var debit  = Posting.Create(txId, SystemAccounts.HeldPoolId, asset, -amount, EntryKind.Debit,  Phase.Void);
        var credit = Posting.Create(txId, debitAccountId,            asset, +amount, EntryKind.Credit, Phase.Void);

        Debug.Assert(debit.AmountSigned + credit.AmountSigned == 0);
        return (debit, credit);
    }

    private static void EnsurePositive(decimal amount)
    {
        if (amount <= 0)
        {
            throw new InvalidAmountException(amount);
        }
    }
}
