using CtxApp.Application.Abstractions;
using CtxApp.Domain.Notifications;

namespace CtxApp.Application.Notifications;

public sealed class NotificationsService(
    INotificationsRepository repository,
    IUnitOfWork unitOfWork,
    IPushSender push,
    IBlindIndex blindIndex) : INotificationsService
{
    public async Task<List<NotificationDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await repository.GetAllAsync(cancellationToken);
        return items.Select(n => new NotificationDto(n.Id, n.Title, n.Body, n.ReadAt, n.CreatedAt)).ToList();
    }

    public Task<int> CountUnreadAsync(CancellationToken cancellationToken = default) =>
        repository.CountUnreadAsync(cancellationToken);

    public async Task<Guid> CreateNotificationAsync(Guid userId, string title, string body, CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Body = body,
        };
        repository.Add(notification);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var tokens = await repository.GetAllDeviceTokensAsync(cancellationToken);
        if (tokens.Count > 0)
        {
            await push.SendAsync(tokens, title, body, cancellationToken);
        }

        return notification.Id;
    }

    public async Task<NotificationDto?> MarkAsReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notification = await repository.GetByIdAsync(id, cancellationToken);
        if (notification is null)
        {
            return null;
        }

        notification.ReadAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new NotificationDto(notification.Id, notification.Title, notification.Body, notification.ReadAt, notification.CreatedAt);
    }

    public async Task RegisterDeviceAsync(Guid userId, string platform, string token, CancellationToken cancellationToken = default)
    {
        var index = blindIndex.Compute(token);
        var existing = await repository.GetDeviceTokenAsync(index, cancellationToken);
        
        if (existing is null)
        {
            repository.AddDeviceToken(new DeviceToken
            {
                UserId = userId,
                Platform = platform,
                Token = token,
                TokenBlindIndex = index,
            });
        }
        else
        {
            existing.Platform = platform;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UnregisterDeviceAsync(string token, CancellationToken cancellationToken = default)
    {
        var index = blindIndex.Compute(token);
        var existing = await repository.GetDeviceTokenAsync(index, cancellationToken);
        
        if (existing is not null)
        {
            repository.RemoveDeviceToken(existing);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
