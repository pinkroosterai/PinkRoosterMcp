using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PinkRooster.Data.Entities;

namespace PinkRooster.Data.Configurations;

public sealed class WorkPackageFeatureRequestLinkConfiguration : IEntityTypeConfiguration<WorkPackageFeatureRequestLink>
{
    public void Configure(EntityTypeBuilder<WorkPackageFeatureRequestLink> builder)
    {
        builder.ToTable("work_package_feature_request_links");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.WorkPackageId).HasColumnName("work_package_id").IsRequired();
        builder.Property(x => x.FeatureRequestId).HasColumnName("feature_request_id").IsRequired();

        // ── Relationships ──
        builder.HasOne(x => x.WorkPackage)
            .WithMany(w => w.LinkedFeatureRequestLinks)
            .HasForeignKey(x => x.WorkPackageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FeatureRequest)
            .WithMany()
            .HasForeignKey(x => x.FeatureRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ──
        builder.HasIndex(x => new { x.WorkPackageId, x.FeatureRequestId }).IsUnique();
        builder.HasIndex(x => x.FeatureRequestId);
    }
}
