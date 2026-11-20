using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFlow.Domain.Common;
using StageFlow.Domain.Entities;

namespace StageFlow.Infrastructure.Persistence.Configurations;

public class InternshipConfiguration : IEntityTypeConfiguration<Internship>
{
    public void Configure(EntityTypeBuilder<Internship> builder)
    {
        builder.ToTable("Internships");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Title)
            .HasMaxLength(FieldLimits.InternshipTitle)
            .IsRequired();

        builder.Property(i => i.SubjectDescription)
            .HasMaxLength(FieldLimits.LongDescription)
            .IsRequired();

        builder.OwnsOne(i => i.Period, period =>
        {
            period.Property(p => p.StartDate)
                .HasColumnName("StartDate")
                .IsRequired();

            period.Property(p => p.EndDate)
                .HasColumnName("EndDate")
                .IsRequired();
        });

        builder.Property(i => i.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.RejectionReason)
            .HasMaxLength(FieldLimits.RejectionReason);

        builder.Property(i => i.StudentId)
            .HasMaxLength(FieldLimits.UserId)
            .IsRequired();

        builder.Property(i => i.AcademicSupervisorId)
            .HasMaxLength(FieldLimits.UserId);

        builder.Property(i => i.CompanySupervisorName)
            .HasMaxLength(FieldLimits.PersonName)
            .IsRequired();

        builder.Property(i => i.CompanySupervisorEmail)
            .HasMaxLength(FieldLimits.Email)
            .IsRequired();

        builder.Property(i => i.CompanySupervisorPhone)
            .HasMaxLength(FieldLimits.Phone);

        builder.HasOne(i => i.Company)
            .WithMany(c => c.Internships)
            .HasForeignKey(i => i.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.AcademicTrack)
            .WithMany(t => t.Internships)
            .HasForeignKey(i => i.AcademicTrackId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.InternshipOffer)
            .WithMany(o => o.Applications)
            .HasForeignKey(i => i.InternshipOfferId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(i => i.StudentId);
        builder.HasIndex(i => i.Status);
    }
}

