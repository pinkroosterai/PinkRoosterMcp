using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PinkRooster.Data.Entities;

namespace PinkRooster.Data.Configurations;

public sealed class WebhookDeliveryLogConfiguration : IEntityTypeConfiguration<WebhookDeliveryLog>
{
    public void Configure(EntityTypeBuilder<WebhookDeliveryLog> builder)
    {
        builder.ToTable("webhook_delivery_logs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.WebhookSubscriptionId).HasColumnName("webhook_subscription_id").IsRequired();

        // ── Event Context ──
        builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(50).IsRequired();
        builder.Property(x => x.EntityId).HasColumnName("entity_id").HasMaxLength(100).IsRequired();

        // ── Delivery Attempt ──
        builder.Property(x => x.AttemptNumber).HasColumnName("attempt_number").HasDefaultValue(1);
        builder.Property(x => x.Payload).HasColumnName("payload").IsRequired();
        builder.Property(x => x.HttpStatusCode).HasColumnName("http_status_code");
        builder.Property(x => x.ResponseBody).HasColumnName("response_body");
        builder.Property(x => x.DurationMs).HasColumnName("duration_ms");
        builder.Property(x => x.Success).HasColumnName("success").HasDefaultValue(false);

        // ── Retry ──
        builder.Property(x => x.NextRetryAt).HasColumnName("next_retry_at");

        // ── Timestamps ──
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        // ── Relationships ──
        builder.HasOne(x => x.Subscription)
            .WithMany(s => s.DeliveryLogs)
            .HasForeignKey(x => x.WebhookSubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ──
        builder.HasIndex(x => x.WebhookSubscriptionId);
        builder.HasIndex(x => x.NextRetryAt);
        builder.HasIndex(x => x.CreatedAt);
    }
}
