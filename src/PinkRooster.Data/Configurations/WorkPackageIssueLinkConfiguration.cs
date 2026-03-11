using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PinkRooster.Data.Entities;

namespace PinkRooster.Data.Configurations;

public sealed class WorkPackageIssueLinkConfiguration : IEntityTypeConfiguration<WorkPackageIssueLink>
{
    public void Configure(EntityTypeBuilder<WorkPackageIssueLink> builder)
    {
        builder.ToTable("work_package_issue_links");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.WorkPackageId).HasColumnName("work_package_id").IsRequired();
        builder.Property(x => x.IssueId).HasColumnName("issue_id").IsRequired();

        // ── Relationships ──
        builder.HasOne(x => x.WorkPackage)
            .WithMany(w => w.LinkedIssueLinks)
            .HasForeignKey(x => x.WorkPackageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Issue)
            .WithMany()
            .HasForeignKey(x => x.IssueId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ──
        builder.HasIndex(x => new { x.WorkPackageId, x.IssueId }).IsUnique();
        builder.HasIndex(x => x.IssueId);
    }
}
