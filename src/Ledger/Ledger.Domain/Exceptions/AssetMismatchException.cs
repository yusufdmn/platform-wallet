namespace PlatformWallet.Ledger.Domain.Exceptions;

public sealed class AssetMismatchException(Guid accountId, string accountAsset, string messageAsset)
    : LedgerDomainException($"Asset mismatch on account {accountId}: account asset='{accountAsset}', message asset='{messageAsset}'.");
