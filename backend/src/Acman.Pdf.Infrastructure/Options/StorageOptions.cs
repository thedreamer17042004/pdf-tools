namespace Acman.Pdf.Infrastructure.Options;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>S3 or FileSystem</summary>
    public string Provider { get; set; } = "FileSystem";

    public string Endpoint { get; set; } = "http://localhost:9000";
    public string AccessKey { get; set; } = "acmanminio";
    public string SecretKey { get; set; } = "acman_minio_secret";
    public string Bucket { get; set; } = "acman-documents";
    public bool ForcePathStyle { get; set; } = true;
    public string? Region { get; set; } = "us-east-1";

    /// <summary>Used when Provider=FileSystem</summary>
    public string FileSystemRoot { get; set; } = "data/objects";
}

public sealed class DocumentsOptions
{
    public const string SectionName = "Documents";
    public long MaxUploadBytes { get; set; } = 50 * 1024 * 1024;
    public TimeSpan RenderCacheTtl { get; set; } = TimeSpan.FromMinutes(30);
}

public sealed class RedisOptions
{
    public const string SectionName = "Redis";
    public string ConnectionString { get; set; } = "localhost:6379";
    public bool Enabled { get; set; } = true;
}
