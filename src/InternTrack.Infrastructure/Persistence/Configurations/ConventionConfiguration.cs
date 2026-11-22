using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using InternTrack.Domain.Common;
using InternTrack.Domain.Entities;

namespace InternTrack.Infrastructure.Persistence.Configurations;

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

        builder.Property(c => c.SignedDocumentStorageKey)
            .HasMaxLength(FieldLimits.StorageKey);

        builder.Property(c => c.SignedDocumentSha256)
            .HasMaxLength(FieldLimits.Sha256);

        builder.Property(c => c.CompanySignerName)
            .HasMaxLength(FieldLimits.PersonName);

        builder.Property(c => c.TerminationReason)
            .HasMaxLength(FieldLimits.RejectionReason);

        builder.Property(c => c.CompanySigningTokenHash)
            .HasMaxLength(FieldLimits.TokenHash);

        // Recherche d'une convention à partir du lien de signature envoyé à l'entreprise.
        builder.HasIndex(c => c.CompanySigningTokenHash)
            .IsUnique()
            .HasFilter("\"CompanySigningTokenHash\" IS NOT NULL");

        builder.HasOne(c => c.Internship)
            .WithOne(i => i.Convention)
            .HasForeignKey<Convention>(c => c.InternshipId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

