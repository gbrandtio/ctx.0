namespace Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);

    /// <summary>
    /// Performs a bcrypt verification against a dummy hash so "user not
    /// found" takes the same time as "wrong password"
    /// (AUTHENTICATION.md — constant-time behaviour).
    /// </summary>
    void DummyVerify();
}
