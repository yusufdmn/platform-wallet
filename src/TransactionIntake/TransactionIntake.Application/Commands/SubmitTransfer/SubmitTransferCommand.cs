using MediatR;

namespace PlatformWallet.TransactionIntake.Application.Commands.SubmitTransfer;

public sealed record SubmitTransferCommand(
    Guid    DebitAccountId,
    Guid    CreditAccountId,
    decimal Amount,
    string  Asset,
    string  IdempotencyKey) : IRequest<SubmitTransferResult>;
