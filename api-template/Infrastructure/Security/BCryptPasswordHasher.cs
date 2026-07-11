using Application.Abstractions;

namespace Infrastructure.Security;

/// <summary>
/// BCrypt with an explicit work factor. DummyVerify burns the same cost
/// when the account does not exist (AUTHENTICATION.md — constant-time
/// behaviour).
/// </summary>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    private static readonly string DummyHash =
        BCrypt.Net.BCrypt.HashPassword("dummy-timing-password", WorkFactor);

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash) =>
        BCrypt.Net.BCrypt.Verify(password, hash);

    public void DummyVerify() => BCrypt.Net.BCrypt.Verify("wrong-password", DummyHash);
}
