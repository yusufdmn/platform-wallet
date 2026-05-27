using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlatformWallet.Ledger.Domain;

namespace PlatformWallet.Ledger.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");

        builder.Property(a => a.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(a => a.Asset).HasColumnName("asset").HasMaxLength(16).IsRequired();
        builder.Property(a => a.Balance).HasColumnName("balance").HasPrecision(28, 8);
        builder.Property(a => a.HeldAmount).HasColumnName("held_amount").HasPrecision(28, 8);
        builder.Property(a => a.IsSystem).HasColumnName("is_system");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");

        var metadataComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<Dictionary<string, string>>(
            (a, b) => System.Text.Json.JsonSerializer.Serialize(a, (System.Text.Json.JsonSerializerOptions?)null)
                   == System.Text.Json.JsonSerializer.Serialize(b, (System.Text.Json.JsonSerializerOptions?)null),
            v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null).GetHashCode(),
            v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                     System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                     (System.Text.Json.JsonSerializerOptions?)null)!);

        builder.Property(a => a.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null)!,
                metadataComparer);

        builder.Property(a => a.RowVersion)
            .HasColumnName("row_version")
            .IsRowVersion()
            .HasDefaultValueSql("'\\x00000000'::bytea");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_accounts_balance_floor",
                "is_system = true OR balance >= 0");
            t.HasCheckConstraint("ck_accounts_held_amount_floor",
                "held_amount >= 0");
        });
    }
}
