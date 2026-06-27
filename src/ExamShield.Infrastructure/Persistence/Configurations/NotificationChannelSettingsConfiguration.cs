using ExamShield.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamShield.Infrastructure.Persistence.Configurations;

public sealed class NotificationChannelSettingsConfiguration
    : IEntityTypeConfiguration<NotificationChannelSettings>
{
    public void Configure(EntityTypeBuilder<NotificationChannelSettings> builder)
    {
        builder.ToTable("NotificationChannelSettings");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EmailRecipients).HasMaxLength(2000).IsRequired(false);
        builder.Property(e => e.SlackWebhookUrl).HasMaxLength(1000).IsRequired(false);
        builder.Property(e => e.LineNotifyToken).HasMaxLength(500).IsRequired(false);
        builder.Property(e => e.WebhookUrl).HasMaxLength(1000).IsRequired(false);

        builder.Property(e => e.UpdatedAt)
            .HasConversion(
                dto => dto.UtcTicks,
                ticks => new DateTimeOffset(ticks, TimeSpan.Zero));
    }
}
