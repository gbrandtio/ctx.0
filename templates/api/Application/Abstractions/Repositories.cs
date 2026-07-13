using Domain.Entities;

namespace Application.Abstractions;

public interface IUserRepository
{
    Task<User?> FindByEmailHashAsync(string emailHash, CancellationToken ct);
    Task<User?> FindByUsernameOrEmailHashAsync(string hash, CancellationToken ct);
    Task<User?> GetByIdAsync(long id, CancellationToken ct);
    Task<bool> EmailHashExistsAsync(string emailHash, CancellationToken ct);
    void Add(User user);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct);
    void Add(RefreshToken token);
    Task RevokeAsync(long tokenId, CancellationToken ct);
    Task RevokeFamilyAsync(Guid familyId, CancellationToken ct);
    Task RevokeAllForUserAsync(long userId, string userType, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface ISignupVerificationRepository
{
    Task<SignupVerification?> FindActiveByEmailHashAsync(string emailHash, CancellationToken ct);
    void Add(SignupVerification verification);
    void Remove(SignupVerification verification);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IGoogleIdentityRepository
{
    Task<UserGoogleIdentity?> FindBySubjectHashAsync(string subjectHash, CancellationToken ct);
    void Add(UserGoogleIdentity identity);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IFirebaseIdentityRepository
{
    Task<UserFirebaseIdentity?> FindByUserIdAsync(long userId, CancellationToken ct);
    void Add(UserFirebaseIdentity identity);
    void Remove(UserFirebaseIdentity identity);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface INotificationRepository
{
    Task<(IReadOnlyList<UserNotification> Items, bool HasMore)> GetPageForUserAsync(
        long userId, int page, int pageSize, CancellationToken ct);

    /// <summary>Outbox write — must share the business operation's transaction.</summary>
    void Add(UserNotification notification);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(long id, CancellationToken ct);
    void Add(Order order);

    /// <summary>
    /// Atomically transitions pending → paid; returns false when another
    /// process already consumed the order (single-use guarantee,
    /// PAYMENTS_STRIPE.md §4).
    /// </summary>
    Task<bool> TryMarkPaidAsync(long orderId, string paymentIntentId, long paidByUserId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface ILedgerRepository
{
    Task<bool> PaymentIntentExistsAsync(string paymentIntentId, CancellationToken ct);
    void Add(LedgerEntry entry);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IItemRepository
{
    Task<IReadOnlyList<(Item Item, double DistanceMeters)>> GetNearbyAsync(
        double latitude, double longitude, double radiusMeters, CancellationToken ct);
}

public interface IUserExportRepository
{
    void Add(UserExport export);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IAppInstanceRepository
{
    Task<AppInstance?> FindByDeviceIdAsync(string deviceId, CancellationToken ct);
    void Add(AppInstance instance);
    Task SaveChangesAsync(CancellationToken ct);
}
