using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlatformWallet.TransactionIntake.Domain;

namespace PlatformWallet.TransactionIntake.Infrastructure.Persistence.Configurations;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    private const int AssetMaxLength   = 16;
    private const int StatusMaxLength  = 16;
    private const int TypeMaxLength    = 16;
    private const int KeyHashMaxLength = 64;

    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.CorrelationId).HasColumnName("correlation_id").IsRequired();
        builder.Property(t => t.Type).HasColumnName("type")
            .HasMaxLength(TypeMaxLength)
            .HasConversion<string>();
        builder.Property(t => t.Status).HasColumnName("status")
            .HasMaxLength(StatusMaxLength)
            .HasConversion<string>();
        builder.Property(t => t.Amount).HasColumnName("amount").HasPrecision(18, 2);
        builder.Property(t => t.Asset).HasColumnName("asset").HasMaxLength(AssetMaxLength).IsRequired();
        builder.Property(t => t.DebitAccountId).HasColumnName("debit_account_id");
        builder.Property(t => t.CreditAccountId).HasColumnName("credit_account_id");
        builder.Property(t => t.IdempotencyKeyHash).HasColumnName("idempotency_key_hash").HasMaxLength(KeyHashMaxLength).IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(t => t.IdempotencyKeyHash)
            .IsUnique()
            .HasDatabaseName("uq_transactions_idempotency_key_hash");

        builder.HasIndex(t => t.CorrelationId)
            .HasDatabaseName("ix_transactions_correlation_id");
    }
}
