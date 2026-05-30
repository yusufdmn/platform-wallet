using PlatformWallet.Ledger.Domain.Exceptions;

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
        EnsurePositive(amount);
        Balance += amount;
    }

    public void ApplyDebit(decimal amount)
    {
        EnsurePositive(amount);
        if (!IsSystem && Balance < amount)
        {
            throw new InsufficientFundsException(Id, amount, Balance);
        }

        Balance -= amount;
    }

    public void ReserveHold(decimal amount)
    {
        EnsurePositive(amount);
        if (!IsSystem && Balance < amount)
        {
            throw new InsufficientFundsException(Id, amount, Balance);
        }

        Balance    -= amount;
        HeldAmount += amount;
    }

    public void ReleaseHold(decimal amount)
    {
        EnsurePositive(amount);
        if (HeldAmount < amount)
        {
            throw new InsufficientHeldAmountException(Id, amount, HeldAmount);
        }

        HeldAmount -= amount;
        Balance    += amount;
    }

    public void CaptureHeld(decimal amount)
    {
        EnsurePositive(amount);
        if (HeldAmount < amount)
        {
            throw new InsufficientHeldAmountException(Id, amount, HeldAmount);
        }

        HeldAmount -= amount;
    }

    private static void EnsurePositive(decimal amount)
    {
        if (amount <= 0)
        {
            throw new InvalidAmountException(amount);
        }
    }
}
