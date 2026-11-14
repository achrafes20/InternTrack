using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFlow.Domain.Entities;

namespace StageFlow.Infrastructure.Persistence.Configurations;

public class DefenseConfiguration : IEntityTypeConfiguration<Defense>
{
    public void Configure(EntityTypeBuilder<Defense> builder)
    {
        builder.ToTable("Defenses");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Classroom)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.FinalGrade)
            .HasPrecision(4, 2);

        builder.Property(d => d.Comments)
            .HasMaxLength(2000);

        builder.HasOne(d => d.Internship)
            .WithOne(i => i.Defense)
            .HasForeignKey<Defense>(d => d.InternshipId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => new { d.Classroom, d.ScheduledAt });
    }
}

