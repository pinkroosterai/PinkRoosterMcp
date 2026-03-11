using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PinkRooster.Data.Entities;

namespace PinkRooster.Data.Configurations;

public sealed class WorkPackageDependencyConfiguration : IEntityTypeConfiguration<WorkPackageDependency>
{
    public void Configure(EntityTypeBuilder<WorkPackageDependency> builder)
    {
        builder.ToTable("work_package_dependencies");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.DependentWorkPackageId).HasColumnName("dependent_work_package_id").IsRequired();
        builder.Property(x => x.DependsOnWorkPackageId).HasColumnName("depends_on_work_package_id").IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);

        // ── Relationships ──
        builder.HasOne(x => x.DependentWorkPackage)
            .WithMany(w => w.BlockedBy)
            .HasForeignKey(x => x.DependentWorkPackageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.DependsOnWorkPackage)
            .WithMany(w => w.Blocking)
            .HasForeignKey(x => x.DependsOnWorkPackageId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ──
        builder.HasIndex(x => new { x.DependentWorkPackageId, x.DependsOnWorkPackageId }).IsUnique();
        builder.HasIndex(x => x.DependsOnWorkPackageId);
    }
}
