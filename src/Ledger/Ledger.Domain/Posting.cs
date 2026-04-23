namespace PlatformWallet.Ledger.Domain;

public class Posting
{
    public long Id { get; private set; }
    public Guid TxId { get; private set; }
    public Guid AccountId { get; private set; }
    public string Asset { get; private set; } = null!;
    public decimal AmountSigned { get; private set; }
    public EntryKind EntryKind { get; private set; }
    public Phase Phase { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Posting() { }

    internal static Posting Create(
        Guid txId, Guid accountId, string asset,
        decimal amountSigned, EntryKind entryKind, Phase phase) =>
        new()
        {
            TxId         = txId,
            AccountId    = accountId,
            Asset        = asset,
            AmountSigned = amountSigned,
            EntryKind    = entryKind,
            Phase        = phase,
            CreatedAt    = DateTimeOffset.UtcNow,
        };
}
