namespace Domain.Entities;

/// <summary>GDPR data export request; completion is notified via push.</summary>
public class UserExport
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Status { get; set; } = Statuses.Pending;
    public DateTime RequestedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public static class Statuses
    {
        public const string Pending = "pending";
        public const string Completed = "completed";
        public const string Failed = "failed";
    }
}
