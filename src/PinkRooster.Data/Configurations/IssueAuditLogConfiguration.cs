using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PinkRooster.Data.Entities;

namespace PinkRooster.Data.Configurations;

public sealed class IssueAuditLogConfiguration : IEntityTypeConfiguration<IssueAuditLog>
{
    public void Configure(EntityTypeBuilder<IssueAuditLog> builder)
    {
        builder.ToTable("issue_audit_logs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.IssueId).HasColumnName("issue_id").IsRequired();
        builder.Property(x => x.FieldName).HasColumnName("field_name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.OldValue).HasColumnName("old_value").HasMaxLength(8000);
        builder.Property(x => x.NewValue).HasColumnName("new_value").HasMaxLength(8000);
        builder.Property(x => x.ChangedBy).HasColumnName("changed_by").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ChangedAt).HasColumnName("changed_at").HasDefaultValueSql("now()");

        // ── Relationships ──
        builder.HasOne(x => x.Issue)
            .WithMany()
            .HasForeignKey(x => x.IssueId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ──
        builder.HasIndex(x => x.IssueId);
    }
}
