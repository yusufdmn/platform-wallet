namespace PlatformWallet.TransactionIntake.Application.Commands.SubmitBurn;

public sealed record SubmitBurnResult(Guid TransactionId, bool WasDuplicate);
