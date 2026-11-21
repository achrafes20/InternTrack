using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFlow.Domain.Common;
using StageFlow.Domain.Entities;

namespace StageFlow.Infrastructure.Persistence.Configurations;

public class InternshipDocumentConfiguration : IEntityTypeConfiguration<InternshipDocument>
{
    public void Configure(EntityTypeBuilder<InternshipDocument> builder)
    {
        builder.ToTable("InternshipDocuments");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Kind)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(d => d.FileName)
            .HasMaxLength(FieldLimits.FileName)
            .IsRequired();

        builder.Property(d => d.ContentType)
            .HasMaxLength(FieldLimits.ContentType)
            .IsRequired();

        builder.Property(d => d.StorageKey)
            .HasMaxLength(FieldLimits.StorageKey)
            .IsRequired();

        builder.HasIndex(d => d.StorageKey)
            .IsUnique();

        builder.Property(d => d.UploadedBy)
            .HasMaxLength(FieldLimits.UserId)
            .IsRequired();

        builder.HasOne(d => d.Internship)
            .WithMany(i => i.Documents)
            .HasForeignKey(d => d.InternshipId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => new { d.InternshipId, d.Kind });
    }
}
