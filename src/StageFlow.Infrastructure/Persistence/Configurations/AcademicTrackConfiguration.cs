using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFlow.Domain.Entities;

namespace StageFlow.Infrastructure.Persistence.Configurations;

public class AcademicTrackConfiguration : IEntityTypeConfiguration<AcademicTrack>
{
    public void Configure(EntityTypeBuilder<AcademicTrack> builder)
    {
        builder.ToTable("AcademicTracks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Code)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(t => t.Code)
            .IsUnique();

        builder.Property(t => t.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasMaxLength(500);
    }
}

