namespace Contracts.Users;

public sealed record UserResponse(
    long Id,
    string Username,
    string Email,
    string? Name,
    bool HasTrackingConsent,
    DateTime CreatedAt);

public sealed record UpdateUserRequest(string? Name, bool? HasTrackingConsent = null);
