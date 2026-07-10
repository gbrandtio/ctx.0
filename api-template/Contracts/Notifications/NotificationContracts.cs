namespace Contracts.Notifications;

public sealed record NotificationResponse(
    long Id,
    string Type,
    string Title,
    string Body,
    DateTime CreatedAt);

public sealed record RegisterFcmTokenRequest(string Token);
