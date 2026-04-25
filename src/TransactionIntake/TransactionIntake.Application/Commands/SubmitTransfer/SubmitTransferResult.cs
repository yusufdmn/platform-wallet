namespace PlatformWallet.TransactionIntake.Application.Commands.SubmitTransfer;

public sealed record SubmitTransferResult(Guid TransactionId, bool WasDuplicate);
