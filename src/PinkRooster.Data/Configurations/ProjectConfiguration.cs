using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PinkRooster.Data.Entities;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Data.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ProjectPath).HasColumnName("project_path").HasMaxLength(1024).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20)
            .HasConversion<string>().HasDefaultValue(ProjectStatus.Active);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        builder.HasIndex(x => x.ProjectPath).IsUnique();
        builder.HasIndex(x => x.Status);
    }
}
