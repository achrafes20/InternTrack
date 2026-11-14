using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFlow.Domain.Entities;

namespace StageFlow.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(c => c.Name);

        builder.Property(c => c.Industry)
            .HasMaxLength(100);

        builder.Property(c => c.City)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Country)
            .HasMaxLength(100)
            .HasDefaultValue("Maroc");

        builder.Property(c => c.Address)
            .HasMaxLength(300);

        builder.Property(c => c.ContactEmail)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.ContactPhone)
            .HasMaxLength(30);

        builder.Property(c => c.Website)
            .HasMaxLength(200);
    }
}

