using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using InternTrack.Infrastructure.Persistence.Outbox;

namespace InternTrack.Infrastructure.Persistence.Configurations;

public class EmailOutboxMessageConfiguration : IEntityTypeConfiguration<EmailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<EmailOutboxMessage> builder)
    {
        builder.ToTable("EmailOutbox");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.ToEmail).HasMaxLength(256).IsRequired();
        builder.Property(m => m.Subject).HasMaxLength(500).IsRequired();
        builder.Property(m => m.HtmlBody).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(1000);
        builder.Property(m => m.Status).HasConversion<int>();

        // Index de la requête de prélèvement : messages en attente dont l'échéance est passée.
        builder.HasIndex(m => new { m.Status, m.NextAttemptAt });
    }
}
