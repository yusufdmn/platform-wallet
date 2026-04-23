namespace PlatformWallet.TransactionIntake.Application.Commands.SubmitMint;

public sealed record SubmitMintResult(Guid TransactionId, bool WasDuplicate);
