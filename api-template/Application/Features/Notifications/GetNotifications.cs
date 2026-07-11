using Application.Abstractions;
using Contracts.Common;
using Contracts.Notifications;
using MediatR;

namespace Application.Features.Notifications;

public sealed record GetNotificationsQuery(long UserId, int Page, int PageSize)
    : IRequest<PagedResponse<NotificationResponse>>;

public sealed class GetNotificationsHandler(INotificationRepository notifications)
    : IRequestHandler<GetNotificationsQuery, PagedResponse<NotificationResponse>>
{
    public async Task<PagedResponse<NotificationResponse>> Handle(
        GetNotificationsQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var (items, hasMore) =
            await notifications.GetPageForUserAsync(query.UserId, page, pageSize, ct);
        return new PagedResponse<NotificationResponse>(
            [.. items.Select(n => new NotificationResponse(
                n.Id, n.Type, n.Title, n.Body, n.CreatedAt))],
            hasMore);
    }
}
