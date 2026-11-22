using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using InternTrack.Domain.Common;
using InternTrack.Domain.Entities;

namespace InternTrack.Infrastructure.Persistence.Configurations;

public class ProgressReportConfiguration : IEntityTypeConfiguration<ProgressReport>
{
    public void Configure(EntityTypeBuilder<ProgressReport> builder)
    {
        builder.ToTable("ProgressReports");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Title)
            .HasMaxLength(FieldLimits.ReportTitle)
            .IsRequired();

        builder.Property(r => r.ActivitiesPerformed)
            .HasMaxLength(FieldLimits.ReportActivities)
            .IsRequired();

        builder.Property(r => r.SkillsAcquired)
            .HasMaxLength(FieldLimits.ReportSection);

        builder.Property(r => r.DifficultiesEncountered)
            .HasMaxLength(FieldLimits.ReportSection);

        builder.Property(r => r.NextWeekGoals)
            .HasMaxLength(FieldLimits.ReportSection);

        builder.Property(r => r.AiSummary)
            .HasMaxLength(FieldLimits.AiSummary);

        builder.Property(r => r.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasOne(r => r.Internship)
            .WithMany(i => i.Reports)
            .HasForeignKey(r => r.InternshipId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.InternshipId, r.WeekNumber })
            .IsUnique();
    }
}

