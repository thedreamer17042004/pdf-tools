using Amazon.S3;
using Amazon.S3.Model;
using Acman.Pdf.Abstractions.Storage;
using Acman.Pdf.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acman.Pdf.Infrastructure.Storage;

public sealed class S3ObjectStorage : IObjectStorage, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;
    private readonly ILogger<S3ObjectStorage> _logger;

    public S3ObjectStorage(IOptions<StorageOptions> options, ILogger<S3ObjectStorage> logger)
    {
        var opts = options.Value;
        _bucket = opts.Bucket;
        _logger = logger;

        var config = new AmazonS3Config
        {
            ServiceURL = opts.Endpoint,
            ForcePathStyle = opts.ForcePathStyle,
            AuthenticationRegion = opts.Region ?? "us-east-1"
        };

        _client = new AmazonS3Client(opts.AccessKey, opts.SecretKey, config);
    }

    public async Task EnsureBucketAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.PutBucketAsync(_bucket, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (
            ex.StatusCode == System.Net.HttpStatusCode.Conflict
            || string.Equals(ex.ErrorCode, "BucketAlreadyOwnedByYou", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ex.ErrorCode, "BucketAlreadyExists", StringComparison.OrdinalIgnoreCase))
        {
            // already exists
        }
    }

    public async Task PutAsync(
        string key,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        };

        await _client.PutObjectAsync(request, cancellationToken);
        _logger.LogDebug("Uploaded object {Key} to bucket {Bucket}", key, _bucket);
    }

    public async Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var response = await _client.GetObjectAsync(_bucket, key, cancellationToken);
        var ms = new MemoryStream();
        await response.ResponseStream.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;
        return ms;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        await _client.DeleteObjectAsync(_bucket, key, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.GetObjectMetadataAsync(_bucket, key, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public void Dispose() => _client.Dispose();
}
