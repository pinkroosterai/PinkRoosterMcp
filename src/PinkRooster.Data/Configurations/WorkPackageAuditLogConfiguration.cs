using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PinkRooster.Data.Entities;

namespace PinkRooster.Data.Configurations;

public sealed class WorkPackageAuditLogConfiguration : IEntityTypeConfiguration<WorkPackageAuditLog>
{
    public void Configure(EntityTypeBuilder<WorkPackageAuditLog> builder)
    {
        builder.ToTable("work_package_audit_logs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.WorkPackageId).HasColumnName("work_package_id").IsRequired();
        builder.Property(x => x.FieldName).HasColumnName("field_name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.OldValue).HasColumnName("old_value").HasMaxLength(8000);
        builder.Property(x => x.NewValue).HasColumnName("new_value").HasMaxLength(8000);
        builder.Property(x => x.ChangedBy).HasColumnName("changed_by").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ChangedAt).HasColumnName("changed_at").HasDefaultValueSql("now()");

        builder.HasOne(x => x.WorkPackage)
            .WithMany()
            .HasForeignKey(x => x.WorkPackageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.WorkPackageId);
    }
}
