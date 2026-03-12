using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PinkRooster.Data.Entities;

namespace PinkRooster.Data.Configurations;

public sealed class ProjectMemoryAuditLogConfiguration : IEntityTypeConfiguration<ProjectMemoryAuditLog>
{
    public void Configure(EntityTypeBuilder<ProjectMemoryAuditLog> builder)
    {
        builder.ToTable("project_memory_audit_logs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.ProjectMemoryId).HasColumnName("project_memory_id").IsRequired();
        builder.Property(x => x.FieldName).HasColumnName("field_name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.OldValue).HasColumnName("old_value");
        builder.Property(x => x.NewValue).HasColumnName("new_value");
        builder.Property(x => x.ChangedBy).HasColumnName("changed_by").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ChangedAt).HasColumnName("changed_at").HasDefaultValueSql("now()");

        // ── Relationships ──
        builder.HasOne(x => x.ProjectMemory)
            .WithMany()
            .HasForeignKey(x => x.ProjectMemoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ──
        builder.HasIndex(x => x.ProjectMemoryId);
    }
}
