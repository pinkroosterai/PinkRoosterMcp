using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PinkRooster.Data.Entities;

namespace PinkRooster.Data.Configurations;

public sealed class FeatureRequestConfiguration : IEntityTypeConfiguration<FeatureRequest>
{
    public void Configure(EntityTypeBuilder<FeatureRequest> builder)
    {
        builder.ToTable("feature_requests");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.FeatureRequestNumber).HasColumnName("feature_request_number").IsRequired();
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();

        // ── Definition ──
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(20)
            .HasConversion<string>();
        builder.Property(x => x.Priority).HasColumnName("priority").HasMaxLength(20)
            .HasConversion<string>();

        // ── Context ──
        builder.Property(x => x.BusinessValue).HasColumnName("business_value").HasMaxLength(4000);
        builder.Property(x => x.UserStory).HasColumnName("user_story").HasMaxLength(4000);
        builder.Property(x => x.Requester).HasColumnName("requester").HasMaxLength(200);
        builder.Property(x => x.AcceptanceSummary).HasColumnName("acceptance_summary").HasMaxLength(4000);

        // ── State ──
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20)
            .HasConversion<string>();
        builder.Property(x => x.StartedAt).HasColumnName("started_at");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.ResolvedAt).HasColumnName("resolved_at");

        // ── Attachments (jsonb) ──
        builder.OwnsMany(x => x.Attachments, a =>
        {
            a.ToJson("attachments");
        });

        // ── Timestamps ──
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        // ── Relationships ──
        builder.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ──
        builder.HasIndex(x => new { x.ProjectId, x.FeatureRequestNumber }).IsUnique();
        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Priority);
        builder.HasIndex(x => x.Category);
    }
}
