using CtxApp.Application.Abstractions;
using CtxApp.Application.Notifications;
using CtxApp.Domain.Notifications;
using CtxApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

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

        group.MapGet("/", async (CtxAppDbContext db) =>
        {
            var items = await db.Set<Notification>().OrderByDescending(n => n.CreatedAt).ToListAsync();
            return Results.Ok(new
            {
                items = items.Select(n => new { n.Id, n.Title, n.Body, n.ReadAt, n.CreatedAt }),
            });
        });

        group.MapGet("/unread-count", async (CtxAppDbContext db) =>
        {
            var count = await db.Set<Notification>().CountAsync(n => n.ReadAt == null);
            return Results.Ok(new { count });
        });

        group.MapPost("/", async (CreateNotificationRequest body, CtxAppDbContext db, ICurrentUser user, IPushSender push, CancellationToken ct) =>
        {
            var notification = new Notification
            {
                UserId = user.UserId!.Value,
                Title = body.Title,
                Body = body.Body,
            };
            db.Set<Notification>().Add(notification);
            await db.SaveChangesAsync(ct);

            // Fan out to the user's registered devices (RLS scopes this to them).
            var tokens = await db.Set<DeviceToken>().Select(d => d.Token).ToListAsync(ct);
            if (tokens.Count > 0)
            {
                await push.SendAsync(tokens, body.Title, body.Body, ct);
            }

            return Results.Ok(new { notification.Id });
        });

        group.MapPost("/{id:guid}/read", async (Guid id, CtxAppDbContext db) =>
        {
            var notification = await db.Set<Notification>().FirstOrDefaultAsync(n => n.Id == id);
            if (notification is null)
            {
                return Results.NotFound();
            }
            notification.ReadAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { notification.Id, notification.ReadAt });
        });

        group.MapPost("/devices", async (RegisterDeviceRequest body, CtxAppDbContext db, ICurrentUser user, IBlindIndex blindIndex, CancellationToken ct) =>
        {
            var index = blindIndex.Compute(body.Token);
            var existing = await db.Set<DeviceToken>().FirstOrDefaultAsync(d => d.TokenBlindIndex == index, ct);
            if (existing is null)
            {
                db.Set<DeviceToken>().Add(new DeviceToken
                {
                    UserId = user.UserId!.Value,
                    Platform = body.Platform,
                    Token = body.Token,
                    TokenBlindIndex = index,
                });
            }
            else
            {
                existing.Platform = body.Platform;
            }
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        group.MapDelete("/devices", async ([FromBody] UnregisterDeviceRequest body, CtxAppDbContext db, IBlindIndex blindIndex, CancellationToken ct) =>
        {
            var index = blindIndex.Compute(body.Token);
            var existing = await db.Set<DeviceToken>().FirstOrDefaultAsync(d => d.TokenBlindIndex == index, ct);
            if (existing is not null)
            {
                db.Set<DeviceToken>().Remove(existing);
                await db.SaveChangesAsync(ct);
            }
            return Results.NoContent();
        });

        return app;
    }
}
