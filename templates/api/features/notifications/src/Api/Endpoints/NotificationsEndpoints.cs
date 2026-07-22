using CtxApp.Application.Abstractions;
using CtxApp.Application.Notifications;
using CtxApp.Domain.Notifications;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CtxApp.Api.Endpoints;

public sealed record CreateNotificationRequest(string Title, string Body);
public sealed record RegisterDeviceRequest(string Platform, string Token);
public sealed record UnregisterDeviceRequest(string Token);

/// <summary>
/// Per-user in-app notifications with encrypted title/body and RLS isolation, plus
/// device-token registration for push delivery. Every query is scoped by RLS to
/// the authenticated user. Requires authentication.
/// </summary>
public static class NotificationsEndpoints
{
    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/notifications").RequireAuthorization();

        group.MapGet("/", async (INotificationsService notificationsService, CancellationToken cancellationToken) =>
        {
            var items = await notificationsService.GetAllAsync(cancellationToken);
            return Results.Ok(new { items });
        });

        group.MapGet("/unread-count", async (INotificationsService notificationsService, CancellationToken cancellationToken) =>
        {
            var count = await notificationsService.CountUnreadAsync(cancellationToken);
            return Results.Ok(new { count });
        });

        group.MapPost("/", async (CreateNotificationRequest body, INotificationsService notificationsService, ICurrentUser user, CancellationToken cancellationToken) =>
        {
            var id = await notificationsService.CreateNotificationAsync(user.UserId!.Value, body.Title, body.Body, cancellationToken);
            return Results.Ok(new { Id = id });
        });

        group.MapPost("/{id:guid}/read", async (Guid id, INotificationsService notificationsService, CancellationToken cancellationToken) =>
        {
            var notification = await notificationsService.MarkAsReadAsync(id, cancellationToken);
            if (notification is null)
            {
                return Results.NotFound();
            }
            return Results.Ok(new { notification.Id, notification.ReadAt });
        });

        group.MapPost("/devices", async (RegisterDeviceRequest body, INotificationsService notificationsService, ICurrentUser user, CancellationToken cancellationToken) =>
        {
            await notificationsService.RegisterDeviceAsync(user.UserId!.Value, body.Platform, body.Token, cancellationToken);
            return Results.NoContent();
        });

        group.MapDelete("/devices", async ([FromBody] UnregisterDeviceRequest body, INotificationsService notificationsService, CancellationToken cancellationToken) =>
        {
            await notificationsService.UnregisterDeviceAsync(body.Token, cancellationToken);
            return Results.NoContent();
        });

        return app;
    }
}
