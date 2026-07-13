using Application.Abstractions;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories.Notifications;

public sealed class NotificationRepository(AppDbContext db) : INotificationRepository
{
    public async Task<(IReadOnlyList<UserNotification> Items, bool HasMore)>
        GetPageForUserAsync(long userId, int page, int pageSize, CancellationToken ct)
    {
        // Fetch one extra row to compute hasMore without a COUNT query.
        var items = await db.UserNotifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize + 1)
            .AsNoTracking()
            .ToListAsync(ct);

        var hasMore = items.Count > pageSize;
        return (items.Take(pageSize).ToList(), hasMore);
    }

    public void Add(UserNotification notification) => db.UserNotifications.Add(notification);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
