using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFlow.Domain.Entities;

namespace StageFlow.Infrastructure.Persistence.Configurations;

public class ProgressReportConfiguration : IEntityTypeConfiguration<ProgressReport>
{
    public void Configure(EntityTypeBuilder<ProgressReport> builder)
    {
        builder.ToTable("ProgressReports");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.ActivitiesPerformed)
            .HasMaxLength(5000)
            .IsRequired();

        builder.Property(r => r.SkillsAcquired)
            .HasMaxLength(2000);

        builder.Property(r => r.DifficultiesEncountered)
            .HasMaxLength(2000);

        builder.Property(r => r.NextWeekGoals)
            .HasMaxLength(2000);

        builder.Property(r => r.AiSummary)
            .HasMaxLength(3000);

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

