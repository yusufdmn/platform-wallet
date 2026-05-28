namespace PlatformWallet.TransactionIntake.Domain;

public class Transaction
{
    public Guid              Id                 { get; private set; }
    public Guid              CorrelationId      { get; private set; }
    public TransactionType   Type               { get; private set; }
    public TransactionStatus Status             { get; private set; }
    public decimal           Amount             { get; private set; }
    public string            Asset              { get; private set; } = null!;
    public Guid?             DebitAccountId     { get; private set; }
    public Guid              CreditAccountId    { get; private set; }
    public string            IdempotencyKeyHash { get; private set; } = null!;
    public DateTimeOffset    CreatedAt          { get; private set; }

    private Transaction() { }

    public static Transaction CreateMint(
        Guid    id,
        Guid    correlationId,
        decimal amount,
        string  asset,
        Guid    creditAccountId,
        string  idempotencyKeyHash)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        return new Transaction
        {
            Id                 = id,
            CorrelationId      = correlationId,
            Type               = TransactionType.Mint,
            Status             = TransactionStatus.Pending,
            Amount             = amount,
            Asset              = asset,
            CreditAccountId    = creditAccountId,
            IdempotencyKeyHash = idempotencyKeyHash,
            CreatedAt          = DateTimeOffset.UtcNow,
        };
    }

    public static Transaction CreateBurn(
        Guid    id,
        Guid    correlationId,
        decimal amount,
        string  asset,
        Guid    debitAccountId,
        string  idempotencyKeyHash)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        return new Transaction
        {
            Id                 = id,
            CorrelationId      = correlationId,
            Type               = TransactionType.Burn,
            Status             = TransactionStatus.Pending,
            Amount             = amount,
            Asset              = asset,
            DebitAccountId     = debitAccountId,
            IdempotencyKeyHash = idempotencyKeyHash,
            CreatedAt          = DateTimeOffset.UtcNow,
        };
    }

    public static Transaction CreateTransfer(
        Guid    id,
        Guid    correlationId,
        decimal amount,
        string  asset,
        Guid    debitAccountId,
        Guid    creditAccountId,
        string  idempotencyKeyHash)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        return new Transaction
        {
            Id                 = id,
            CorrelationId      = correlationId,
            Type               = TransactionType.Transfer,
            Status             = TransactionStatus.Pending,
            Amount             = amount,
            Asset              = asset,
            DebitAccountId     = debitAccountId,
            CreditAccountId    = creditAccountId,
            IdempotencyKeyHash = idempotencyKeyHash,
            CreatedAt          = DateTimeOffset.UtcNow,
        };
    }

    public void Transition(TransactionStatus newStatus) => Status = newStatus;
}
