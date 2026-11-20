using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFlow.Domain.Common;
using StageFlow.Domain.Entities;

namespace StageFlow.Infrastructure.Persistence.Configurations;

public class AcademicTrackConfiguration : IEntityTypeConfiguration<AcademicTrack>
{
    public void Configure(EntityTypeBuilder<AcademicTrack> builder)
    {
        builder.ToTable("AcademicTracks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Code)
            .HasMaxLength(FieldLimits.TrackCode)
            .IsRequired();

        builder.HasIndex(t => t.Code)
            .IsUnique();

        builder.Property(t => t.Name)
            .HasMaxLength(FieldLimits.TrackName)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasMaxLength(FieldLimits.TrackDescription);
    }
}

