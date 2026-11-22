using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using InternTrack.Domain.Entities;

namespace InternTrack.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Token)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(r => r.Token)
            .IsUnique();

        builder.Property(r => r.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.HasIndex(r => r.UserId);
    }
}

