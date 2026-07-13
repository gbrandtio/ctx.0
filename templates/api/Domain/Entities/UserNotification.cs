namespace Domain.Entities;

/// <summary>
/// Transactional outbox row + in-app feed item (NOTIFICATIONS.md §1, §4).
/// Inserted in the same transaction as the business change; NULL sent_at
/// is the only "pending" signal.
/// </summary>
public class UserNotification
{
    public long Id { get; set; }
    public long UserId { get; set; }

    /// <summary>Discriminator the client uses to render/handle the payload.</summary>
    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
}
