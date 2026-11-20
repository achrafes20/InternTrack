using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFlow.Domain.Common;
using StageFlow.Domain.Entities;

namespace StageFlow.Infrastructure.Persistence.Configurations;

public class ConventionConfiguration : IEntityTypeConfiguration<Convention>
{
    public void Configure(EntityTypeBuilder<Convention> builder)
    {
        builder.ToTable("Conventions");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ConventionNumber)
            .HasMaxLength(FieldLimits.ConventionNumber)
            .IsRequired();

        builder.HasIndex(c => c.ConventionNumber)
            .IsUnique();

        builder.Property(c => c.VerificationToken)
            .HasMaxLength(FieldLimits.VerificationToken)
            .IsRequired();

        builder.HasIndex(c => c.VerificationToken)
            .IsUnique();

        builder.HasOne(c => c.Internship)
            .WithOne(i => i.Convention)
            .HasForeignKey<Convention>(c => c.InternshipId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

