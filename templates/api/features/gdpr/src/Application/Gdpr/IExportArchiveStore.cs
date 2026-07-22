namespace CtxApp.Application.Gdpr;

public interface IExportArchiveStore
{
    Task WriteAsync(string key, byte[] content, CancellationToken ct = default);
    Task<byte[]> ReadAsync(string key, CancellationToken ct = default);
    void Delete(string key);
    bool Exists(string key);
}
