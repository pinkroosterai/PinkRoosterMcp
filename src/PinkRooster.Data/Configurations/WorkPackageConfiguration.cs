using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PinkRooster.Data.Entities;

namespace PinkRooster.Data.Configurations;

public sealed class WorkPackageConfiguration : IEntityTypeConfiguration<WorkPackage>
{
    public void Configure(EntityTypeBuilder<WorkPackage> builder)
    {
        builder.ToTable("work_packages");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.WorkPackageNumber).HasColumnName("work_package_number").IsRequired();
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        // ── Definition ──
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Type).HasColumnName("type").HasMaxLength(20)
            .HasConversion<string>();
        builder.Property(x => x.Priority).HasColumnName("priority").HasMaxLength(20)
            .HasConversion<string>();
        builder.Property(x => x.Plan).HasColumnName("plan").HasMaxLength(16000);

        // ── Estimation ──
        builder.Property(x => x.EstimatedComplexity).HasColumnName("estimated_complexity");
        builder.Property(x => x.EstimationRationale).HasColumnName("estimation_rationale").HasMaxLength(4000);

        // ── State ──
        builder.Property(x => x.State).HasColumnName("state").HasMaxLength(20)
            .HasConversion<string>();
        builder.Property(x => x.PreviousActiveState).HasColumnName("previous_active_state").HasMaxLength(20)
            .HasConversion<string?>();
        builder.Property(x => x.StartedAt).HasColumnName("started_at");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.ResolvedAt).HasColumnName("resolved_at");

        // ── Attachments (jsonb) ──
        builder.OwnsMany(x => x.Attachments, a =>
        {
            a.ToJson("attachments");
        });

        // ── Sequential number counters ──
        builder.Property(x => x.NextPhaseNumber).HasColumnName("next_phase_number").HasDefaultValue(1);
        builder.Property(x => x.NextTaskNumber).HasColumnName("next_task_number").HasDefaultValue(1);

        // ── Timestamps ──
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        // ── Relationships ──
        builder.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ──
        builder.HasIndex(x => new { x.ProjectId, x.WorkPackageNumber }).IsUnique();
        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.State);
        builder.HasIndex(x => x.Priority);
        builder.HasIndex(x => x.Type);
    }
}
