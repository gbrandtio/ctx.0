using CtxApp.Application.Notifications;
using CtxApp.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Infrastructure.Persistence;

public sealed class NotificationsRepository(CtxAppDbContext db) : INotificationsRepository
{
    public Task<List<Notification>> GetAllAsync(CancellationToken ct = default) =>
        db.Set<Notification>().OrderByDescending(n => n.CreatedAt).ToListAsync(ct);

    public Task<int> CountUnreadAsync(CancellationToken ct = default) =>
        db.Set<Notification>().CountAsync(n => n.ReadAt == null, ct);

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Set<Notification>().FirstOrDefaultAsync(n => n.Id == id, ct);

    public void Add(Notification notification) => db.Set<Notification>().Add(notification);

    public Task<DeviceToken?> GetDeviceTokenAsync(string tokenBlindIndex, CancellationToken ct = default) =>
        db.Set<DeviceToken>().FirstOrDefaultAsync(d => d.TokenBlindIndex == tokenBlindIndex, ct);

    public Task<List<string>> GetAllDeviceTokensAsync(CancellationToken ct = default) =>
        db.Set<DeviceToken>().Select(d => d.Token).ToListAsync(ct);

    public void AddDeviceToken(DeviceToken deviceToken) => db.Set<DeviceToken>().Add(deviceToken);

    public void RemoveDeviceToken(DeviceToken deviceToken) => db.Set<DeviceToken>().Remove(deviceToken);
}
