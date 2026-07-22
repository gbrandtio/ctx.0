using CtxApp.Domain.Notifications;

namespace CtxApp.Application.Notifications;

public interface INotificationsRepository
{
    Task<List<Notification>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<int> CountUnreadAsync(CancellationToken cancellationToken = default);
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Add(Notification notification);

    Task<DeviceToken?> GetDeviceTokenAsync(string tokenBlindIndex, CancellationToken cancellationToken = default);
    Task<List<string>> GetAllDeviceTokensAsync(CancellationToken cancellationToken = default);
    void AddDeviceToken(DeviceToken deviceToken);
    void RemoveDeviceToken(DeviceToken deviceToken);
}
