using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PinkRooster.Data.Entities;

namespace PinkRooster.Data.Configurations;

public sealed class WorkPackagePhaseConfiguration : IEntityTypeConfiguration<WorkPackagePhase>
{
    public void Configure(EntityTypeBuilder<WorkPackagePhase> builder)
    {
        builder.ToTable("work_package_phases");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.PhaseNumber).HasColumnName("phase_number").IsRequired();
        builder.Property(x => x.WorkPackageId).HasColumnName("work_package_id").IsRequired();

        // ── Definition ──
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(4000);
        builder.Property(x => x.SortOrder).HasColumnName("sort_order");

        // ── State ──
        builder.Property(x => x.State).HasColumnName("state").HasMaxLength(20)
            .HasConversion<string>();

        // ── Timestamps ──
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        // ── Relationships ──
        builder.HasOne(x => x.WorkPackage)
            .WithMany(w => w.Phases)
            .HasForeignKey(x => x.WorkPackageId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ──
        builder.HasIndex(x => new { x.WorkPackageId, x.PhaseNumber }).IsUnique();
        builder.HasIndex(x => x.WorkPackageId);
    }
}
