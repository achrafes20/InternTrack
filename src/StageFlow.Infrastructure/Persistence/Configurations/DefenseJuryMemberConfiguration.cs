using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFlow.Domain.Entities;

namespace StageFlow.Infrastructure.Persistence.Configurations;

public class DefenseJuryMemberConfiguration : IEntityTypeConfiguration<DefenseJuryMember>
{
    public void Configure(EntityTypeBuilder<DefenseJuryMember> builder)
    {
        builder.ToTable("DefenseJuryMembers");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.TeacherId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(m => m.TeacherFullName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(m => m.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.HasOne(m => m.Defense)
            .WithMany(d => d.JuryMembers)
            .HasForeignKey(m => m.DefenseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.DefenseId, m.TeacherId })
            .IsUnique();
    }
}

