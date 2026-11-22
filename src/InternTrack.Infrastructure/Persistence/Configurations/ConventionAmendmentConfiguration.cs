using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using InternTrack.Domain.Common;
using InternTrack.Domain.Entities;

namespace InternTrack.Infrastructure.Persistence.Configurations;

public class ConventionAmendmentConfiguration : IEntityTypeConfiguration<ConventionAmendment>
{
    public void Configure(EntityTypeBuilder<ConventionAmendment> builder)
    {
        builder.ToTable("ConventionAmendments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Reason).HasMaxLength(FieldLimits.RejectionReason).IsRequired();
        builder.Property(a => a.RequestedBy).HasMaxLength(FieldLimits.UserId).IsRequired();
        builder.Property(a => a.NewTitle).HasMaxLength(FieldLimits.InternshipTitle);
        builder.Property(a => a.NewSubjectDescription).HasMaxLength(FieldLimits.LongDescription);
        builder.Property(a => a.NewCompanySupervisorName).HasMaxLength(FieldLimits.PersonName);
        builder.Property(a => a.NewCompanySupervisorEmail).HasMaxLength(FieldLimits.Email);
        builder.Property(a => a.NewCompanySupervisorPhone).HasMaxLength(FieldLimits.Phone);
        builder.Property(a => a.ChangesSummary).HasMaxLength(FieldLimits.LongDescription).IsRequired();
        builder.Property(a => a.CompanySignerName).HasMaxLength(FieldLimits.PersonName);
        builder.Property(a => a.WithdrawalReason).HasMaxLength(FieldLimits.RejectionReason);

        // Numérotation unique par convention (deux créations concurrentes ne peuvent pas obtenir le même numéro).
        builder.HasIndex(a => new { a.ConventionId, a.Number }).IsUnique();

        // Un seul avenant en attente par convention, garanti au niveau base.
        builder.HasIndex(a => a.ConventionId)
            .IsUnique()
            .HasFilter("\"AppliedAt\" IS NULL AND \"WithdrawnAt\" IS NULL")
            .HasDatabaseName("IX_ConventionAmendments_SinglePending");

        builder.HasOne(a => a.Convention)
            .WithMany(c => c.Amendments)
            .HasForeignKey(a => a.ConventionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
