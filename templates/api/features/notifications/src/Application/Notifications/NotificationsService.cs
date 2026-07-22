using CtxApp.Application.Abstractions;
using CtxApp.Domain.Notifications;

namespace CtxApp.Application.Notifications;

public sealed class NotificationsService(
    INotificationsRepository repository,
    IUnitOfWork unitOfWork,
    IPushSender push,
    IBlindIndex blindIndex) : INotificationsService
{
    public async Task<List<NotificationDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await repository.GetAllAsync(ct);
        return items.Select(n => new NotificationDto(n.Id, n.Title, n.Body, n.ReadAt, n.CreatedAt)).ToList();
    }

    public Task<int> CountUnreadAsync(CancellationToken ct = default) =>
        repository.CountUnreadAsync(ct);

    public async Task<Guid> CreateNotificationAsync(Guid userId, string title, string body, CancellationToken ct = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Body = body,
        };
        repository.Add(notification);
        await unitOfWork.SaveChangesAsync(ct);

        var tokens = await repository.GetAllDeviceTokensAsync(ct);
        if (tokens.Count > 0)
        {
            await push.SendAsync(tokens, title, body, ct);
        }

        return notification.Id;
    }

    public async Task<NotificationDto?> MarkAsReadAsync(Guid id, CancellationToken ct = default)
    {
        var notification = await repository.GetByIdAsync(id, ct);
        if (notification is null)
        {
            return null;
        }

        notification.ReadAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);

        return new NotificationDto(notification.Id, notification.Title, notification.Body, notification.ReadAt, notification.CreatedAt);
    }

    public async Task RegisterDeviceAsync(Guid userId, string platform, string token, CancellationToken ct = default)
    {
        var index = blindIndex.Compute(token);
        var existing = await repository.GetDeviceTokenAsync(index, ct);
        
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

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task UnregisterDeviceAsync(string token, CancellationToken ct = default)
    {
        var index = blindIndex.Compute(token);
        var existing = await repository.GetDeviceTokenAsync(index, ct);
        
        if (existing is not null)
        {
            repository.RemoveDeviceToken(existing);
            await unitOfWork.SaveChangesAsync(ct);
        }
    }
}
