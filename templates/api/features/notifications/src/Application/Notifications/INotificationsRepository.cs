using CtxApp.Domain.Notifications;

namespace CtxApp.Application.Notifications;

public interface INotificationsRepository
{
    Task<List<Notification>> GetAllAsync(CancellationToken ct = default);
    Task<int> CountUnreadAsync(CancellationToken ct = default);
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);
    void Add(Notification notification);

    Task<DeviceToken?> GetDeviceTokenAsync(string tokenBlindIndex, CancellationToken ct = default);
    Task<List<string>> GetAllDeviceTokensAsync(CancellationToken ct = default);
    void AddDeviceToken(DeviceToken deviceToken);
    void RemoveDeviceToken(DeviceToken deviceToken);
}
