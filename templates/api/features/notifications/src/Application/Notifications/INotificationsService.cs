using CtxApp.Domain.Notifications;

namespace CtxApp.Application.Notifications;

public sealed record NotificationDto(Guid Id, string Title, string Body, DateTimeOffset? ReadAt, DateTimeOffset CreatedAt);

public interface INotificationsService
{
    Task<List<NotificationDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<int> CountUnreadAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateNotificationAsync(Guid userId, string title, string body, CancellationToken cancellationToken = default);
    Task<NotificationDto?> MarkAsReadAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task RegisterDeviceAsync(Guid userId, string platform, string token, CancellationToken cancellationToken = default);
    Task UnregisterDeviceAsync(string token, CancellationToken cancellationToken = default);
}
