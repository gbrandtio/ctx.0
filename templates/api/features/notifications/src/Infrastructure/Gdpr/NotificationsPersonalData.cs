using CtxApp.Application.Abstractions;
using CtxApp.Domain.Notifications;
using CtxApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Infrastructure.Gdpr;

/// <summary>
/// The notifications feature's personal data: the user's in-app notifications and
/// their registered push devices. Device tokens are delivery credentials, so the
/// export carries only the platform and when it was registered — never the token
/// or its blind index — while erasure removes both tables' rows.
/// </summary>
public sealed class NotificationsPersonalData(CtxAppDbContext dbContext) : IPersonalDataContributor
{
    public string Section => "notifications";

    public async Task<object?> ExportAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var messages = await dbContext.Set<Notification>()
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderBy(n => n.CreatedAt)
            .Select(n => new { n.Id, n.Title, n.Body, n.ReadAt, n.CreatedAt })
            .ToListAsync(cancellationToken);

        var devices = await dbContext.Set<DeviceToken>()
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderBy(d => d.CreatedAt)
            .Select(d => new { d.Id, d.Platform, d.CreatedAt })
            .ToListAsync(cancellationToken);

        return messages.Count == 0 && devices.Count == 0
            ? null
            : new { Messages = messages, Devices = devices };
    }

    public async Task EraseAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var messages = await dbContext.Set<Notification>().Where(n => n.UserId == userId).ToListAsync(cancellationToken);
        dbContext.Set<Notification>().RemoveRange(messages);

        var devices = await dbContext.Set<DeviceToken>().Where(d => d.UserId == userId).ToListAsync(cancellationToken);
        dbContext.Set<DeviceToken>().RemoveRange(devices);
    }
}
