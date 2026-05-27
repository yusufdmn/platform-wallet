using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Events;
using PlatformWallet.TransactionIntake.Application.Common;
using PlatformWallet.TransactionIntake.Application.Persistence;
using PlatformWallet.TransactionIntake.Domain;

namespace PlatformWallet.TransactionIntake.Application.Commands.SubmitMint;

public sealed class SubmitMintHandler(
    ITransactionRepository  transactionRepo,
    IIdempotencyRepository  idempotencyRepo,
    IPublishEndpoint        publishEndpoint,
    ILogger<SubmitMintHandler> logger)
    : IRequestHandler<SubmitMintCommand, SubmitMintResult>
{
    public async Task<SubmitMintResult> Handle(
        SubmitMintCommand request,
        CancellationToken cancellationToken)
    {
        var keyHash = IdempotencyHash.Compute(request.IdempotencyKey);

        var existing = await idempotencyRepo.FindTransactionIdAsync(keyHash, cancellationToken);
        if (existing.HasValue)
        {
            return DuplicateResult(existing.Value);
        }

        return await CreateAndPersistAsync(request, keyHash, cancellationToken);
    }

    private async Task<SubmitMintResult> CreateAndPersistAsync(
        SubmitMintCommand request,
        string            keyHash,
        CancellationToken cancellationToken)
    {
        var transactionId = NewId.NextGuid();
        var transaction   = BuildTransaction(request, transactionId, keyHash);

        transactionRepo.Add(transaction);
        idempotencyRepo.Add(keyHash, transactionId);

        await PublishMintFundsAsync(request, transactionId, cancellationToken);
        await transactionRepo.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Mint submitted: TransactionId={TransactionId} Amount={Amount} {Asset} CreditAccount={AccountId}",
            transactionId, request.Amount, request.Asset, request.CreditAccountId);

        return new SubmitMintResult(transactionId, WasDuplicate: false);
    }

    private static Transaction BuildTransaction(
        SubmitMintCommand request,
        Guid              transactionId,
        string            keyHash) =>
        Transaction.CreateMint(
            id:                 transactionId,
            correlationId:      transactionId,
            amount:             request.Amount,
            asset:              request.Asset,
            creditAccountId:    request.CreditAccountId,
            idempotencyKeyHash: keyHash);

    private async Task PublishMintFundsAsync(
        SubmitMintCommand request,
        Guid              transactionId,
        CancellationToken cancellationToken) =>
        await publishEndpoint.Publish(
            new TransactionSubmitted(
                transactionId,
                TransactionType.Mint.ToString(),
                DebitAccountId:  Guid.Empty,
                CreditAccountId: request.CreditAccountId,
                request.Amount,
                request.Asset),
            cancellationToken);

    private static SubmitMintResult DuplicateResult(Guid transactionId) =>
        new(transactionId, WasDuplicate: true);
}
