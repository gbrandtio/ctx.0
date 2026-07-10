namespace Domain.Entities;

/// <summary>Google account link; the Google `sub` claim is stored hashed.</summary>
public class UserGoogleIdentity
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string GoogleSubjectHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
