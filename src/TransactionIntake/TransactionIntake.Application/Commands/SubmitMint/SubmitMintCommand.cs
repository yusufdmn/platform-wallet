using MediatR;

namespace PlatformWallet.TransactionIntake.Application.Commands.SubmitMint;

public sealed record SubmitMintCommand(
    Guid    CreditAccountId,
    decimal Amount,
    string  Asset,
    string  IdempotencyKey) : IRequest<SubmitMintResult>;
