namespace PlatformWallet.Ledger.Domain;

public class Account
{
    public Guid Id { get; private set; }
    public string? Name { get; private set; }
    public string Asset { get; private set; } = null!;
    public decimal Balance { get; private set; }
    public decimal HeldAmount { get; private set; }
    public bool IsSystem { get; private set; }
    public Dictionary<string, string> Metadata { get; private set; } = [];
    public byte[] RowVersion { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }

    private Account() { }

    public static Account Create(Guid id, string? name, string asset, bool isSystem = false) =>
        new()
        {
            Id        = id,
            Name      = name,
            Asset     = asset,
            IsSystem  = isSystem,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    public void ApplyCredit(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        Balance += amount;
    }

    public void ApplyDebit(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        if (!IsSystem && Balance < amount)
        {
            throw new InvalidOperationException(
                $"Insufficient balance on account {Id}: balance={Balance}, requested={amount}");
        }

        Balance -= amount;
    }

    public void ReserveHold(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        if (!IsSystem && Balance < amount)
        {
            throw new InvalidOperationException(
                $"Insufficient balance to hold on account {Id}: balance={Balance}, requested={amount}");
        }

        Balance    -= amount;
        HeldAmount += amount;
    }

    public void ReleaseHold(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        HeldAmount -= amount;
        Balance    += amount;
    }

    public void CaptureHeld(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        HeldAmount -= amount;
    }
}
