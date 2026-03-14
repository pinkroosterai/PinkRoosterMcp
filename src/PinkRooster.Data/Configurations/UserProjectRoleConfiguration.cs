using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PinkRooster.Data.Entities;

namespace PinkRooster.Data.Configurations;

public sealed class UserProjectRoleConfiguration : IEntityTypeConfiguration<UserProjectRole>
{
    public void Configure(EntityTypeBuilder<UserProjectRole> builder)
    {
        builder.ToTable("user_project_roles");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.Role).HasColumnName("role").HasMaxLength(20)
            .HasConversion<string>();

        // ── Timestamps ──
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        // ── Relationships ──
        builder.HasOne(x => x.User)
            .WithMany(u => u.ProjectRoles)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ──
        builder.HasIndex(x => new { x.UserId, x.ProjectId }).IsUnique();
        builder.HasIndex(x => x.ProjectId);
    }
}
