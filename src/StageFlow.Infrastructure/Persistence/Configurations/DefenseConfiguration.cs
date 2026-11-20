using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFlow.Domain.Common;
using StageFlow.Domain.Entities;

namespace StageFlow.Infrastructure.Persistence.Configurations;

public class DefenseConfiguration : IEntityTypeConfiguration<Defense>
{
    public void Configure(EntityTypeBuilder<Defense> builder)
    {
        builder.ToTable("Defenses");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Classroom)
            .HasMaxLength(FieldLimits.Classroom)
            .IsRequired();

        builder.Property(d => d.FinalGrade)
            .HasPrecision(4, 2);

        builder.Property(d => d.Comments)
            .HasMaxLength(FieldLimits.DefenseComments);

        builder.HasOne(d => d.Internship)
            .WithOne(i => i.Defense)
            .HasForeignKey<Defense>(d => d.InternshipId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(d => d.EndsAt)
            .IsRequired();

        builder.HasIndex(d => new { d.Classroom, d.ScheduledAt });

        // Recherche des chevauchements (ScheduledAt < fin demandée ET EndsAt > début demandé).
        builder.HasIndex(d => new { d.ScheduledAt, d.EndsAt });

        // La contrainte d'exclusion PostgreSQL « EX_Defenses_Classroom_Slot » (btree_gist) interdit
        // physiquement deux soutenances qui se chevauchent dans la même salle. Elle est créée en SQL
        // dans la migration AddConcurrencyAndDefenseSlotConstraint (non exprimable via l'API Fluent).
    }
}

