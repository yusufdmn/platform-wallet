using MediatR;

namespace PlatformWallet.TransactionIntake.Application.Commands.SubmitBurn;

public sealed record SubmitBurnCommand(
    Guid    DebitAccountId,
    decimal Amount,
    string  Asset,
    string  IdempotencyKey) : IRequest<SubmitBurnResult>;
