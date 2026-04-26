using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PlatformWallet.WebhookDispatcher.Infrastructure.Persistence.Configurations;

internal sealed class FailedWebhookDeliveryConfiguration : IEntityTypeConfiguration<FailedWebhookDelivery>
{
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

        builder.HasIndex(x => x.CorrelationId);
    }
}
