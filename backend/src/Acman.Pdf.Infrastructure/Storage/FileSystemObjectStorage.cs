using Acman.Pdf.Abstractions.Storage;
using Acman.Pdf.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acman.Pdf.Infrastructure.Storage;

public sealed class FileSystemObjectStorage : IObjectStorage
{
    private readonly string _root;
    private readonly ILogger<FileSystemObjectStorage> _logger;

    public FileSystemObjectStorage(IOptions<StorageOptions> options, ILogger<FileSystemObjectStorage> logger)
    {
        _root = Path.GetFullPath(options.Value.FileSystemRoot);
        _logger = logger;
        Directory.CreateDirectory(_root);
    }

    public Task EnsureBucketAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root);
        return Task.CompletedTask;
    }

    public async Task PutAsync(
        string key,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var fs = File.Create(path);
        await content.CopyToAsync(fs, cancellationToken);
        _logger.LogDebug("Stored object {Key} ({ContentType}) at {Path}", key, contentType, path);
    }

    public Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Object '{key}' was not found in file storage.", path);
        }

        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(File.Exists(ResolvePath(key)));

    private string ResolvePath(string key)
    {
        var safe = key.Replace('\\', '/').TrimStart('/');
        if (safe.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Object key must not contain path traversal segments.");
        }

        var full = Path.GetFullPath(Path.Combine(_root, safe.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Object key resolves outside storage root.");
        }

        return full;
    }
}
