using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlatformWallet.TransactionIntake.Infrastructure.Persistence.Outbox;

namespace PlatformWallet.TransactionIntake.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    private const int KeyHashMaxLength = 64;

    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.ToTable("idempotency_keys");

        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id).HasColumnName("id");
        builder.Property(k => k.KeyHash).HasColumnName("key_hash").HasMaxLength(KeyHashMaxLength).IsRequired();
        builder.Property(k => k.TransactionId).HasColumnName("transaction_id").IsRequired();
        builder.Property(k => k.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(k => k.KeyHash)
            .IsUnique()
            .HasDatabaseName("uq_idempotency_keys_key_hash");
    }
}
