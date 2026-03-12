using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PinkRooster.Data.Entities;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Data.Configurations;

public sealed class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> builder)
    {
        builder.ToTable("issues");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.IssueNumber).HasColumnName("issue_number").IsRequired();
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();

        // ── Definition ──
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").IsRequired();
        builder.Property(x => x.IssueType).HasColumnName("issue_type").HasMaxLength(30)
            .HasConversion<string>();
        builder.Property(x => x.Severity).HasColumnName("severity").HasMaxLength(20)
            .HasConversion<string>();
        builder.Property(x => x.Priority).HasColumnName("priority").HasMaxLength(20)
            .HasConversion<string>();

        // ── Reproduction / diagnosis ──
        builder.Property(x => x.StepsToReproduce).HasColumnName("steps_to_reproduce");
        builder.Property(x => x.ExpectedBehavior).HasColumnName("expected_behavior");
        builder.Property(x => x.ActualBehavior).HasColumnName("actual_behavior");
        builder.Property(x => x.AffectedComponent).HasColumnName("affected_component").HasMaxLength(500);
        builder.Property(x => x.StackTrace).HasColumnName("stack_trace");

        // ── Resolution ──
        builder.Property(x => x.RootCause).HasColumnName("root_cause");
        builder.Property(x => x.Resolution).HasColumnName("resolution");

        // ── State ──
        builder.Property(x => x.State).HasColumnName("state").HasMaxLength(20)
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
        builder.HasIndex(x => new { x.ProjectId, x.IssueNumber }).IsUnique();
        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.State);
        builder.HasIndex(x => x.Priority);
        builder.HasIndex(x => x.Severity);
    }
}
