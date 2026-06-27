using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ExamShield.Infrastructure.Persistence.Repositories;

public sealed class NotificationChannelSettingsRepository(ExamShieldDbContext db)
    : INotificationChannelSettingsRepository
{
    public async Task<NotificationChannelSettings> GetAsync(CancellationToken ct = default)
    {
        var settings = await db.NotificationChannelSettings.FirstOrDefaultAsync(ct);
        if (settings is not null) return settings;

        settings = NotificationChannelSettings.CreateDefault();
        db.NotificationChannelSettings.Add(settings);
        await db.SaveChangesAsync(ct);
        return settings;
    }

    public async Task SaveAsync(NotificationChannelSettings settings, CancellationToken ct = default)
    {
        db.NotificationChannelSettings.Update(settings);
        await db.SaveChangesAsync(ct);
    }
}
