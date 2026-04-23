namespace PlatformWallet.TransactionIntake.Infrastructure.Persistence.Outbox;

public class IdempotencyKey
{
    public Guid            Id            { get; private set; }
    public string          KeyHash       { get; private set; } = null!;
    public Guid            TransactionId { get; private set; }
    public DateTimeOffset  CreatedAt     { get; private set; }

    private IdempotencyKey() { }

    public static IdempotencyKey Create(string keyHash, Guid transactionId) =>
        new()
        {
            Id            = Guid.NewGuid(),
            KeyHash       = keyHash,
            TransactionId = transactionId,
            CreatedAt     = DateTimeOffset.UtcNow,
        };
}
