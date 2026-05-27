using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlatformWallet.SagaOrchestrator.Domain;

namespace PlatformWallet.SagaOrchestrator.Infrastructure.Persistence.Configurations;

public sealed class TransactionSagaStateConfiguration : IEntityTypeConfiguration<TransactionSagaState>
{
    private const int MaxStateLength          = 64;
    private const int MaxTransactionTypeLength = 32;
    private const int MaxAssetLength           = 16;
    private const int MaxReasonLength          = 512;

    public void Configure(EntityTypeBuilder<TransactionSagaState> builder)
    {
        builder.ToTable("transaction_saga_states");

        builder.HasKey(x => x.CorrelationId);
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id");

        builder.Property(x => x.CurrentState)
            .HasColumnName("current_state")
            .HasMaxLength(MaxStateLength)
            .IsRequired();

        builder.Property(x => x.TransactionType)
            .HasColumnName("transaction_type")
            .HasMaxLength(MaxTransactionTypeLength)
            .IsRequired();

        builder.Property(x => x.DebitAccountId).HasColumnName("debit_account_id");
        builder.Property(x => x.CreditAccountId).HasColumnName("credit_account_id");
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(28, 8);

        builder.Property(x => x.Asset)
            .HasColumnName("asset")
            .HasMaxLength(MaxAssetLength)
            .IsRequired();

        builder.Property(x => x.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(MaxReasonLength);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.Property(x => x.RowVersion)
            .HasColumnName("row_version")
            .IsConcurrencyToken();

        builder.HasIndex(x => x.CurrentState).HasDatabaseName("IX_transaction_saga_states_current_state");
    }
}
