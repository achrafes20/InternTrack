using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using InternTrack.Domain.Common;
using InternTrack.Domain.Entities;

namespace InternTrack.Infrastructure.Persistence.Configurations;

public class DefenseEvaluationConfiguration : IEntityTypeConfiguration<DefenseEvaluation>
{
    public void Configure(EntityTypeBuilder<DefenseEvaluation> builder)
    {
        builder.ToTable("DefenseEvaluations");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TeacherId)
            .HasMaxLength(FieldLimits.UserId)
            .IsRequired();

        builder.Property(e => e.TeacherFullName)
            .HasMaxLength(FieldLimits.PersonName)
            .IsRequired();

        builder.Property(e => e.Grade)
            .HasPrecision(4, 2);

        builder.Property(e => e.Comment)
            .HasMaxLength(FieldLimits.DefenseComments);

        builder.HasOne(e => e.Defense)
            .WithMany(d => d.Evaluations)
            .HasForeignKey(e => e.DefenseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Une seule évaluation par enseignant et par soutenance (elle est révisée, jamais dupliquée).
        builder.HasIndex(e => new { e.DefenseId, e.TeacherId })
            .IsUnique();
    }
}
