using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Events;
using PlatformWallet.TransactionIntake.Application.Common;
using PlatformWallet.TransactionIntake.Application.Persistence;
using PlatformWallet.TransactionIntake.Domain;

namespace PlatformWallet.TransactionIntake.Application.Commands.SubmitBurn;

public sealed class SubmitBurnHandler(
    ITransactionRepository  transactionRepo,
    IIdempotencyRepository  idempotencyRepo,
    IPublishEndpoint        publishEndpoint,
    ILogger<SubmitBurnHandler> logger)
    : IRequestHandler<SubmitBurnCommand, SubmitBurnResult>
{
    public async Task<SubmitBurnResult> Handle(
        SubmitBurnCommand request,
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

    private async Task<SubmitBurnResult> CreateAndPersistAsync(
        SubmitBurnCommand request,
        string            keyHash,
        CancellationToken cancellationToken)
    {
        var transactionId = NewId.NextGuid();
        var transaction   = BuildTransaction(request, transactionId, keyHash);

        transactionRepo.Add(transaction);
        idempotencyRepo.Add(keyHash, transactionId);

        await PublishBurnFundsAsync(request, transactionId, cancellationToken);
        await transactionRepo.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Burn submitted: TransactionId={TransactionId} Amount={Amount} {Asset} DebitAccount={AccountId}",
            transactionId, request.Amount, request.Asset, request.DebitAccountId);

        return new SubmitBurnResult(transactionId, WasDuplicate: false);
    }

    private static Transaction BuildTransaction(
        SubmitBurnCommand request,
        Guid              transactionId,
        string            keyHash) =>
        Transaction.CreateBurn(
            id:                 transactionId,
            correlationId:      transactionId,
            amount:             request.Amount,
            asset:              request.Asset,
            debitAccountId:     request.DebitAccountId,
            idempotencyKeyHash: keyHash);

    private async Task PublishBurnFundsAsync(
        SubmitBurnCommand request,
        Guid              transactionId,
        CancellationToken cancellationToken) =>
        await publishEndpoint.Publish(
            new TransactionSubmitted(
                transactionId,
                TransactionType.Burn.ToString(),
                DebitAccountId:  request.DebitAccountId,
                CreditAccountId: Guid.Empty,
                request.Amount,
                request.Asset),
            cancellationToken);

    private static SubmitBurnResult DuplicateResult(Guid transactionId) =>
        new(transactionId, WasDuplicate: true);
}
