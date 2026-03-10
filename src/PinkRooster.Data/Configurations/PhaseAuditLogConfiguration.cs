using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PinkRooster.Data.Entities;

namespace PinkRooster.Data.Configurations;

public sealed class PhaseAuditLogConfiguration : IEntityTypeConfiguration<PhaseAuditLog>
{
    public void Configure(EntityTypeBuilder<PhaseAuditLog> builder)
    {
        builder.ToTable("phase_audit_logs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.PhaseId).HasColumnName("phase_id").IsRequired();
        builder.Property(x => x.FieldName).HasColumnName("field_name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.OldValue).HasColumnName("old_value").HasMaxLength(8000);
        builder.Property(x => x.NewValue).HasColumnName("new_value").HasMaxLength(8000);
        builder.Property(x => x.ChangedBy).HasColumnName("changed_by").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ChangedAt).HasColumnName("changed_at").HasDefaultValueSql("now()");

        builder.HasOne(x => x.Phase)
            .WithMany()
            .HasForeignKey(x => x.PhaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.PhaseId);
    }
}
