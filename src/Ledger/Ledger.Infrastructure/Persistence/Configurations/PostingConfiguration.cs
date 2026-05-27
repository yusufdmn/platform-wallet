using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlatformWallet.Ledger.Domain;

namespace PlatformWallet.Ledger.Infrastructure.Persistence.Configurations;

internal sealed class PostingConfiguration : IEntityTypeConfiguration<Posting>
{
    public void Configure(EntityTypeBuilder<Posting> builder)
    {
        builder.ToTable("postings");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(p => p.TxId).HasColumnName("tx_id");
        builder.Property(p => p.AccountId).HasColumnName("account_id");
        builder.Property(p => p.Asset).HasColumnName("asset").HasMaxLength(16).IsRequired();
        builder.Property(p => p.AmountSigned).HasColumnName("amount_signed").HasPrecision(28, 8);
        builder.Property(p => p.EntryKind).HasColumnName("entry_kind").HasConversion<string>().HasMaxLength(16);
        builder.Property(p => p.Phase).HasColumnName("phase").HasConversion<string>().HasMaxLength(16);
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(p => p.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.TxId, p.AccountId, p.Phase })
            .IsUnique()
            .HasDatabaseName("uq_posting_tx_account_phase");

        builder.HasIndex(p => new { p.AccountId, p.CreatedAt })
            .HasDatabaseName("ix_postings_account_created")
            .IsDescending(false, true);

        builder.HasIndex(p => p.TxId)
            .HasDatabaseName("ix_postings_tx");
    }
}
