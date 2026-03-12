using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PinkRooster.Data.Entities;

namespace PinkRooster.Data.Configurations;

public sealed class AcceptanceCriterionConfiguration : IEntityTypeConfiguration<AcceptanceCriterion>
{
    public void Configure(EntityTypeBuilder<AcceptanceCriterion> builder)
    {
        builder.ToTable("acceptance_criteria");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.PhaseId).HasColumnName("phase_id").IsRequired();

        // ── Definition ──
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").IsRequired();
        builder.Property(x => x.VerificationMethod).HasColumnName("verification_method").HasMaxLength(20)
            .HasConversion<string>();

        // ── Verification ──
        builder.Property(x => x.VerificationResult).HasColumnName("verification_result");
        builder.Property(x => x.VerifiedAt).HasColumnName("verified_at");

        // ── Relationships ──
        builder.HasOne(x => x.Phase)
            .WithMany(p => p.AcceptanceCriteria)
            .HasForeignKey(x => x.PhaseId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ──
        builder.HasIndex(x => x.PhaseId);
    }
}
