using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using InternTrack.Domain.Common;
using InternTrack.Domain.Entities;

namespace InternTrack.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .HasMaxLength(FieldLimits.CompanyName)
            .IsRequired();

        builder.HasIndex(c => c.Name);

        builder.Property(c => c.Industry)
            .HasMaxLength(FieldLimits.Industry);

        builder.Property(c => c.City)
            .HasMaxLength(FieldLimits.City)
            .IsRequired();

        builder.Property(c => c.Country)
            .HasMaxLength(FieldLimits.Country)
            .HasDefaultValue("Maroc");

        builder.Property(c => c.Address)
            .HasMaxLength(FieldLimits.Address);

        builder.Property(c => c.ContactEmail)
            .HasMaxLength(FieldLimits.Email)
            .IsRequired();

        builder.Property(c => c.ContactPhone)
            .HasMaxLength(FieldLimits.Phone);

        builder.Property(c => c.Website)
            .HasMaxLength(FieldLimits.Website);
    }
}

