namespace Acman.Pdf.Abstractions.Storage;

public interface IObjectStorage
{
    Task EnsureBucketAsync(CancellationToken cancellationToken = default);

    Task PutAsync(
        string key,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}

public interface IRenderCache
{
    Task<byte[]?> GetAsync(string cacheKey, CancellationToken cancellationToken = default);
    Task SetAsync(string cacheKey, byte[] value, TimeSpan ttl, CancellationToken cancellationToken = default);
}
