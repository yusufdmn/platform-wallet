using MassTransit;

namespace PlatformWallet.SagaOrchestrator.Domain;

public sealed class TransactionSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId  { get; set; }
    public string CurrentState { get; set; } = null!;
    public string TransactionType { get; set; } = null!;
    public Guid?  DebitAccountId  { get; set; }
    public Guid   CreditAccountId { get; set; }
    public decimal Amount         { get; set; }
    public string Asset           { get; set; } = null!;
    public string? FailureReason  { get; set; }
    public bool IsCompensating    { get; set; }
    public int  VoidAttempts      { get; set; }
    public Guid? HoldExpiryTokenId { get; set; }
    public DateTimeOffset CreatedAt  { get; set; }
    public DateTimeOffset UpdatedAt  { get; set; }
    public byte[] RowVersion        { get; set; } = [];
}
