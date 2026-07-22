namespace CtxApp.Application.Gdpr;

public interface IExportArchiveStore
{
    Task WriteAsync(string key, byte[] content, CancellationToken cancellationToken = default);
    Task<byte[]> ReadAsync(string key, CancellationToken cancellationToken = default);
    void Delete(string key);
    bool Exists(string key);
}
