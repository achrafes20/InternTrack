using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageFlow.Domain.Entities;

namespace StageFlow.Infrastructure.Persistence.Configurations;

public class ReportCommentConfiguration : IEntityTypeConfiguration<ReportComment>
{
    public void Configure(EntityTypeBuilder<ReportComment> builder)
    {
        builder.ToTable("ReportComments");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Content)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(c => c.AuthorId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(c => c.AuthorFullName)
            .HasMaxLength(150)
            .IsRequired();

        builder.HasOne(c => c.ProgressReport)
            .WithMany(r => r.Comments)
            .HasForeignKey(c => c.ProgressReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

