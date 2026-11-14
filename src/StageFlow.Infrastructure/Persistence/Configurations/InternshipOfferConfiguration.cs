using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFlow.Domain.Entities;

namespace StageFlow.Infrastructure.Persistence.Configurations;

public class InternshipOfferConfiguration : IEntityTypeConfiguration<InternshipOffer>
{
    public void Configure(EntityTypeBuilder<InternshipOffer> builder)
    {
        builder.ToTable("InternshipOffers");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Title)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(o => o.Description)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(o => o.RequiredSkills)
            .HasMaxLength(500);

        builder.Property(o => o.City)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(o => o.MonthlyStipend)
            .HasPrecision(10, 2);

        builder.HasOne(o => o.Company)
            .WithMany(c => c.Offers)
            .HasForeignKey(o => o.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.AcademicTrack)
            .WithMany(t => t.Offers)
            .HasForeignKey(o => o.AcademicTrackId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => o.IsActive);
        builder.HasIndex(o => o.City);
    }
}

