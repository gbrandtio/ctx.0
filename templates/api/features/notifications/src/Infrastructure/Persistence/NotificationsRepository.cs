using CtxApp.Application.Notifications;
using CtxApp.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Infrastructure.Persistence;

public sealed class NotificationsRepository(CtxAppDbContext dbContext) : INotificationsRepository
{
    public Task<List<Notification>> GetAllAsync(CancellationToken cancellationToken = default) =>
        dbContext.Set<Notification>().OrderByDescending(n => n.CreatedAt).ToListAsync(cancellationToken);

    public Task<int> CountUnreadAsync(CancellationToken cancellationToken = default) =>
        dbContext.Set<Notification>().CountAsync(n => n.ReadAt == null, cancellationToken);

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Set<Notification>().FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

    public void Add(Notification notification) => dbContext.Set<Notification>().Add(notification);

    public Task<DeviceToken?> GetDeviceTokenAsync(string tokenBlindIndex, CancellationToken cancellationToken = default) =>
        dbContext.Set<DeviceToken>().FirstOrDefaultAsync(d => d.TokenBlindIndex == tokenBlindIndex, cancellationToken);

    public Task<List<string>> GetAllDeviceTokensAsync(CancellationToken cancellationToken = default) =>
        dbContext.Set<DeviceToken>().Select(d => d.Token).ToListAsync(cancellationToken);

    public void AddDeviceToken(DeviceToken deviceToken) => dbContext.Set<DeviceToken>().Add(deviceToken);

    public void RemoveDeviceToken(DeviceToken deviceToken) => dbContext.Set<DeviceToken>().Remove(deviceToken);
}
