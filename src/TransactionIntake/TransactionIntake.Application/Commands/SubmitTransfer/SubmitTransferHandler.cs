using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Events;
using PlatformWallet.TransactionIntake.Application.Commands.SubmitMint;
using PlatformWallet.TransactionIntake.Application.Persistence;
using PlatformWallet.TransactionIntake.Domain;

namespace PlatformWallet.TransactionIntake.Application.Commands.SubmitTransfer;

public sealed class SubmitTransferHandler(
    ITransactionRepository     transactionRepo,
    IIdempotencyRepository     idempotencyRepo,
    IPublishEndpoint           publishEndpoint,
    ILogger<SubmitTransferHandler> logger)
    : IRequestHandler<SubmitTransferCommand, SubmitTransferResult>
{
    public async Task<SubmitTransferResult> Handle(
        SubmitTransferCommand request,
        CancellationToken     cancellationToken)
    {
        var keyHash = IdempotencyHash.Compute(request.IdempotencyKey);

        var existing = await idempotencyRepo.FindTransactionIdAsync(keyHash, cancellationToken);
        if (existing.HasValue)
        {
            return new SubmitTransferResult(existing.Value, WasDuplicate: true);
        }

        return await CreateAndPersistAsync(request, keyHash, cancellationToken);
    }

    private async Task<SubmitTransferResult> CreateAndPersistAsync(
        SubmitTransferCommand request,
        string                keyHash,
        CancellationToken     cancellationToken)
    {
        var transactionId = NewId.NextGuid();
        var transaction   = BuildTransaction(request, transactionId, keyHash);

        transactionRepo.Add(transaction);
        idempotencyRepo.Add(keyHash, transactionId);

        await PublishTransactionSubmittedAsync(request, transactionId, cancellationToken);
        await transactionRepo.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Transfer submitted: TransactionId={TransactionId} Amount={Amount} {Asset} Debit={DebitId} Credit={CreditId}",
            transactionId, request.Amount, request.Asset, request.DebitAccountId, request.CreditAccountId);

        return new SubmitTransferResult(transactionId, WasDuplicate: false);
    }

    private static Transaction BuildTransaction(
        SubmitTransferCommand request,
        Guid                  transactionId,
        string                keyHash) =>
        Transaction.CreateTransfer(
            id:                 transactionId,
            correlationId:      transactionId,
            amount:             request.Amount,
            asset:              request.Asset,
            debitAccountId:     request.DebitAccountId,
            creditAccountId:    request.CreditAccountId,
            idempotencyKeyHash: keyHash);

    private async Task PublishTransactionSubmittedAsync(
        SubmitTransferCommand request,
        Guid                  transactionId,
        CancellationToken     cancellationToken) =>
        await publishEndpoint.Publish(
            new TransactionSubmitted(
                transactionId,
                TransactionType.Transfer.ToString(),
                request.DebitAccountId,
                request.CreditAccountId,
                request.Amount,
                request.Asset),
            cancellationToken);
}
