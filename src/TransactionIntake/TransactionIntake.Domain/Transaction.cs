using PlatformWallet.TransactionIntake.Domain.Exceptions;

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

    private static readonly IReadOnlyDictionary<TransactionStatus, IReadOnlySet<TransactionStatus>> AllowedTransitions =
        new Dictionary<TransactionStatus, IReadOnlySet<TransactionStatus>>
        {
            [TransactionStatus.Pending]          = new HashSet<TransactionStatus> { TransactionStatus.Held,
                                                                                    TransactionStatus.Captured,
                                                                                    TransactionStatus.Failed },
            [TransactionStatus.Held]             = new HashSet<TransactionStatus> { TransactionStatus.CaptureRequested,
                                                                                    TransactionStatus.VoidRequested,
                                                                                    TransactionStatus.Failed },
            [TransactionStatus.CaptureRequested] = new HashSet<TransactionStatus> { TransactionStatus.Captured,
                                                                                    TransactionStatus.Failed },
            [TransactionStatus.VoidRequested]    = new HashSet<TransactionStatus> { TransactionStatus.Voided,
                                                                                    TransactionStatus.Failed },
            [TransactionStatus.Captured]         = new HashSet<TransactionStatus>(),
            [TransactionStatus.Voided]           = new HashSet<TransactionStatus>(),
            [TransactionStatus.Failed]           = new HashSet<TransactionStatus>(),
        };

    private Transaction() { }

    public static Transaction CreateMint(
        Guid    id,
        Guid    correlationId,
        decimal amount,
        string  asset,
        Guid    creditAccountId,
        string  idempotencyKeyHash)
    {
        EnsurePositive(amount);

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
        EnsurePositive(amount);

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
        EnsurePositive(amount);

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

    public void Transition(TransactionStatus newStatus)
    {
        if (Status == newStatus)
        {
            return;
        }

        if (!AllowedTransitions[Status].Contains(newStatus))
        {
            throw new InvalidTransitionException(Status, newStatus);
        }

        Status = newStatus;
    }

    private static void EnsurePositive(decimal amount)
    {
        if (amount <= 0)
        {
            throw new InvalidAmountException(amount);
        }
    }
}
