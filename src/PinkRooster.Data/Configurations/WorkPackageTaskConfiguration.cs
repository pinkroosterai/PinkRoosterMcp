using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PinkRooster.Data.Entities;

namespace PinkRooster.Data.Configurations;

public sealed class WorkPackageTaskConfiguration : IEntityTypeConfiguration<WorkPackageTask>
{
    public void Configure(EntityTypeBuilder<WorkPackageTask> builder)
    {
        builder.ToTable("work_package_tasks");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.TaskNumber).HasColumnName("task_number").IsRequired();
        builder.Property(x => x.PhaseId).HasColumnName("phase_id").IsRequired();
        builder.Property(x => x.WorkPackageId).HasColumnName("work_package_id").IsRequired();

        // ── Definition ──
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order");
        builder.Property(x => x.ImplementationNotes).HasColumnName("implementation_notes");

        // ── State ──
        builder.Property(x => x.State).HasColumnName("state").HasMaxLength(20)
            .HasConversion<string>();
        builder.Property(x => x.PreviousActiveState).HasColumnName("previous_active_state").HasMaxLength(20)
            .HasConversion<string?>();
        builder.Property(x => x.StartedAt).HasColumnName("started_at");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.ResolvedAt).HasColumnName("resolved_at");

        // ── Files (jsonb) ──
        builder.OwnsMany(x => x.TargetFiles, a =>
        {
            a.ToJson("target_files");
        });
        builder.OwnsMany(x => x.Attachments, a =>
        {
            a.ToJson("attachments");
        });

        // ── Timestamps ──
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        // ── Relationships ──
        builder.HasOne(x => x.Phase)
            .WithMany(p => p.Tasks)
            .HasForeignKey(x => x.PhaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.WorkPackage)
            .WithMany()
            .HasForeignKey(x => x.WorkPackageId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ──
        builder.HasIndex(x => new { x.WorkPackageId, x.TaskNumber }).IsUnique();
        builder.HasIndex(x => x.PhaseId);
        builder.HasIndex(x => x.WorkPackageId);
        builder.HasIndex(x => x.State);
    }
}
