using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PinkRooster.Data.Entities;

namespace PinkRooster.Data.Configurations;

public sealed class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.ToTable("webhook_subscriptions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();

        // ── Configuration ──
        builder.Property(x => x.Url).HasColumnName("url").HasMaxLength(2048).IsRequired();
        builder.Property(x => x.Secret).HasColumnName("secret").HasMaxLength(255).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        // ── Event Filters (jsonb) ──
        builder.OwnsMany(x => x.EventFilters, ef =>
        {
            ef.ToJson("event_filters");
        });

        // ── Delivery State ──
        builder.Property(x => x.LastDeliveredAt).HasColumnName("last_delivered_at");
        builder.Property(x => x.LastFailedAt).HasColumnName("last_failed_at");
        builder.Property(x => x.ConsecutiveFailures).HasColumnName("consecutive_failures").HasDefaultValue(0);

        // ── Timestamps ──
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        // ── Relationships ──
        builder.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ──
        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.IsActive);
    }
}
