using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PinkRooster.Data.Entities;

namespace PinkRooster.Data.Configurations;

public sealed class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("activity_logs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.HttpMethod).HasColumnName("http_method").HasMaxLength(10).IsRequired();
        builder.Property(x => x.Path).HasColumnName("path").HasMaxLength(2048).IsRequired();
        builder.Property(x => x.StatusCode).HasColumnName("status_code");
        builder.Property(x => x.DurationMs).HasColumnName("duration_ms");
        builder.Property(x => x.CallerIdentity).HasColumnName("caller_identity").HasMaxLength(256);
        builder.Property(x => x.Timestamp).HasColumnName("timestamp").HasDefaultValueSql("now()");

        builder.HasIndex(x => x.Timestamp).IsDescending();
        builder.HasIndex(x => x.CallerIdentity);
    }
}
