using CtxApp.Domain.Notifications;

namespace CtxApp.Application.Notifications;

public sealed record NotificationDto(Guid Id, string Title, string Body, DateTimeOffset? ReadAt, DateTimeOffset CreatedAt);

public interface INotificationsService
{
    Task<List<NotificationDto>> GetAllAsync(CancellationToken ct = default);
    Task<int> CountUnreadAsync(CancellationToken ct = default);
    Task<Guid> CreateNotificationAsync(Guid userId, string title, string body, CancellationToken ct = default);
    Task<NotificationDto?> MarkAsReadAsync(Guid id, CancellationToken ct = default);
    
    Task RegisterDeviceAsync(Guid userId, string platform, string token, CancellationToken ct = default);
    Task UnregisterDeviceAsync(string token, CancellationToken ct = default);
}
