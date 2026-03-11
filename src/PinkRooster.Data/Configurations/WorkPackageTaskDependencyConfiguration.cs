using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PinkRooster.Data.Entities;

namespace PinkRooster.Data.Configurations;

public sealed class WorkPackageTaskDependencyConfiguration : IEntityTypeConfiguration<WorkPackageTaskDependency>
{
    public void Configure(EntityTypeBuilder<WorkPackageTaskDependency> builder)
    {
        builder.ToTable("work_package_task_dependencies");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.DependentTaskId).HasColumnName("dependent_task_id").IsRequired();
        builder.Property(x => x.DependsOnTaskId).HasColumnName("depends_on_task_id").IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);

        // ── Relationships ──
        builder.HasOne(x => x.DependentTask)
            .WithMany(t => t.BlockedBy)
            .HasForeignKey(x => x.DependentTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.DependsOnTask)
            .WithMany(t => t.Blocking)
            .HasForeignKey(x => x.DependsOnTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ──
        builder.HasIndex(x => new { x.DependentTaskId, x.DependsOnTaskId }).IsUnique();
        builder.HasIndex(x => x.DependsOnTaskId);
    }
}
