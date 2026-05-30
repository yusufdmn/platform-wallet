using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PlatformWallet.WebhookDispatcher.Infrastructure.Persistence.Configurations;

internal sealed class FailedWebhookDeliveryConfiguration : IEntityTypeConfiguration<FailedWebhookDelivery>
{
    private const int MaxStatusLength = 16;

    public void Configure(EntityTypeBuilder<FailedWebhookDelivery> builder)
    {
        builder.ToTable("failed_webhook_deliveries");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();

        builder.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CorrelationId).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.LastHttpStatusCode);
        builder.Property(x => x.LastHttpResponseBody).HasMaxLength(8192);
        builder.Property(x => x.FailedAt).IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(MaxStatusLength)
            .HasDefaultValue(FailedDeliveryStatus.Failed)
            .IsRequired();

        builder.Property(x => x.RetryCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.RetriedAt);

        builder.HasIndex(x => x.CorrelationId);
        builder.HasIndex(x => x.Status);
    }
}
