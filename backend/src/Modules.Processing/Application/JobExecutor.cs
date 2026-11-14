using System.IO.Compression;
using System.Text.Json;
using Acman.Pdf.Abstractions.Contracts;
using Acman.Pdf.Abstractions.Models;
using Acman.Pdf.Abstractions.Storage;
using PageRange = Acman.Pdf.Abstractions.Contracts.PageRange;
using CompressOptions = Acman.Pdf.Abstractions.Contracts.CompressOptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Documents.Infrastructure;
using Modules.Processing.Domain;
using Modules.Processing.Infrastructure;

namespace Modules.Processing.Application;

public interface IJobExecutor
{
    Task ExecuteAsync(Guid jobId, CancellationToken cancellationToken = default);
}

public sealed class JobExecutor : IJobExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ProcessingDbContext _processingDb;
    private readonly DocumentsDbContext _documentsDb;
    private readonly IObjectStorage _storage;
    private readonly IPdfManipulationService _tools;
    private readonly IPdfRenderer _renderer;
    private readonly ILogger<JobExecutor> _logger;

    public JobExecutor(
        ProcessingDbContext processingDb,
        DocumentsDbContext documentsDb,
        IObjectStorage storage,
        IPdfManipulationService tools,
        IPdfRenderer renderer,
        ILogger<JobExecutor> logger)
    {
        _processingDb = processingDb;
        _documentsDb = documentsDb;
        _storage = storage;
        _tools = tools;
        _renderer = renderer;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _processingDb.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken)
                  ?? throw new InvalidOperationException($"Job {jobId} not found.");

        job.Status = ProcessingJobStatus.Running;
        job.StartedAtUtc = DateTimeOffset.UtcNow;
        job.Error = null;
        await _processingDb.SaveChangesAsync(cancellationToken);

        try
        {
            switch (job.Type)
            {
                case ProcessingJobType.Merge:
                    await ExecuteMergeAsync(job, cancellationToken);
                    break;
                case ProcessingJobType.Split:
                    await ExecuteSplitAsync(job, cancellationToken);
                    break;
                case ProcessingJobType.Rotate:
                    await ExecuteRotateAsync(job, cancellationToken);
                    break;
                case ProcessingJobType.Compress:
                    await ExecuteCompressAsync(job, cancellationToken);
                    break;
                case ProcessingJobType.Organize:
                    await ExecuteOrganizeAsync(job, cancellationToken);
                    break;
                case ProcessingJobType.Watermark:
                    await ExecuteWatermarkAsync(job, cancellationToken);
                    break;
                case ProcessingJobType.Protect:
                    await ExecuteProtectAsync(job, cancellationToken);
                    break;
                case ProcessingJobType.ExtractPages:
                    await ExecuteExtractPagesAsync(job, cancellationToken);
                    break;
                case ProcessingJobType.Reverse:
                    await ExecuteReverseAsync(job, cancellationToken);
                    break;
                case ProcessingJobType.DeletePages:
                    await ExecuteDeletePagesAsync(job, cancellationToken);
                    break;
                case ProcessingJobType.PageNumbers:
                    await ExecutePageNumbersAsync(job, cancellationToken);
                    break;
                case ProcessingJobType.Unlock:
                    await ExecuteUnlockAsync(job, cancellationToken);
                    break;
                case ProcessingJobType.PdfToImages:
                    await ExecutePdfToImagesAsync(job, cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported job type {job.Type}");
            }

            job.Status = ProcessingJobStatus.Succeeded;
            job.CompletedAtUtc = DateTimeOffset.UtcNow;
            await _processingDb.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Job {JobId} succeeded", jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed", jobId);
            job.Status = ProcessingJobStatus.Failed;
            job.Error = ex.Message.Length > 3900 ? ex.Message[..3900] : ex.Message;
            job.CompletedAtUtc = DateTimeOffset.UtcNow;
            await _processingDb.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ExecuteMergeAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<MergeJobPayload>(job.PayloadJson, JsonOptions)
                      ?? throw new InvalidOperationException("Invalid merge payload.");
        if (payload.DocumentIds.Count < 2)
        {
            throw new InvalidOperationException("Merge requires at least two documents.");
        }

        var streams = new List<Stream>();
        try
        {
            foreach (var docId in payload.DocumentIds)
            {
                var entity = await _documentsDb.Documents.FindAsync([docId], cancellationToken)
                             ?? throw new InvalidOperationException($"Document {docId} not found.");
                streams.Add(await _storage.GetAsync(entity.StorageKey, cancellationToken));
            }

            var merged = await _tools.MergeAsync(streams, cancellationToken);
            await StoreResultAsync(job, merged, payload.OutputFileName, "application/pdf", cancellationToken);
        }
        finally
        {
            foreach (var s in streams)
            {
                await s.DisposeAsync();
            }
        }
    }

    private async Task ExecuteSplitAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<SplitJobPayload>(job.PayloadJson, JsonOptions)
                      ?? throw new InvalidOperationException("Invalid split payload.");
        var entity = await _documentsDb.Documents.FindAsync([payload.DocumentId], cancellationToken)
                     ?? throw new InvalidOperationException($"Document {payload.DocumentId} not found.");

        await using var stream = await _storage.GetAsync(entity.StorageKey, cancellationToken);
        var ranges = payload.Ranges.Count == 0
            ? null
            : payload.Ranges.Select(r => new PageRange(r.From, r.To)).ToList();

        var parts = await _tools.SplitAsync(stream, ranges, cancellationToken);

        if (parts.Count == 1)
        {
            await StoreResultAsync(job, parts[0], $"{payload.OutputFileNamePrefix}.pdf", "application/pdf", cancellationToken);
            return;
        }

        await using var zipMs = new MemoryStream();
        using (var zip = new ZipArchive(zipMs, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (var i = 0; i < parts.Count; i++)
            {
                var entry = zip.CreateEntry($"{payload.OutputFileNamePrefix}-{i + 1}.pdf", CompressionLevel.Fastest);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(parts[i], cancellationToken);
            }
        }

        await StoreResultAsync(job, zipMs.ToArray(), $"{payload.OutputFileNamePrefix}.zip", "application/zip", cancellationToken);
    }

    private async Task ExecuteRotateAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<RotateJobPayload>(job.PayloadJson, JsonOptions)
                      ?? throw new InvalidOperationException("Invalid rotate payload.");
        var entity = await _documentsDb.Documents.FindAsync([payload.DocumentId], cancellationToken)
                     ?? throw new InvalidOperationException($"Document {payload.DocumentId} not found.");

        await using var stream = await _storage.GetAsync(entity.StorageKey, cancellationToken);
        var rotated = await _tools.RotateAsync(
            stream,
            payload.Degrees,
            payload.PageIndexes.Count == 0 ? null : payload.PageIndexes,
            cancellationToken);

        await StoreResultAsync(job, rotated, payload.OutputFileName, "application/pdf", cancellationToken);
    }

    private async Task ExecuteCompressAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<CompressJobPayload>(job.PayloadJson, JsonOptions)
                      ?? throw new InvalidOperationException("Invalid compress payload.");
        var entity = await _documentsDb.Documents.FindAsync([payload.DocumentId], cancellationToken)
                     ?? throw new InvalidOperationException($"Document {payload.DocumentId} not found.");

        await using var stream = await _storage.GetAsync(entity.StorageKey, cancellationToken);
        var result = await _tools.CompressAsync(
            stream,
            new CompressOptions(RemoveMetadata: payload.RemoveMetadata),
            cancellationToken);

        await StoreResultAsync(job, result.Bytes, payload.OutputFileName, "application/pdf", cancellationToken);
        _logger.LogInformation(
            "Compress job {JobId}: {Original} -> {Compressed}",
            job.Id, result.OriginalSizeBytes, result.CompressedSizeBytes);
    }

    private async Task ExecuteOrganizeAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<OrganizeJobPayload>(job.PayloadJson, JsonOptions)
                      ?? throw new InvalidOperationException("Invalid organize payload.");
        if (payload.PageOrder.Count == 0)
        {
            throw new InvalidOperationException("Organize requires a non-empty pageOrder.");
        }

        var entity = await _documentsDb.Documents.FindAsync([payload.DocumentId], cancellationToken)
                     ?? throw new InvalidOperationException($"Document {payload.DocumentId} not found.");

        await using var stream = await _storage.GetAsync(entity.StorageKey, cancellationToken);
        var organized = await _tools.OrganizeAsync(stream, payload.PageOrder, cancellationToken);
        await StoreResultAsync(job, organized, payload.OutputFileName, "application/pdf", cancellationToken);
    }

    private async Task ExecuteWatermarkAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<WatermarkJobPayload>(job.PayloadJson, JsonOptions)
                      ?? throw new InvalidOperationException("Invalid watermark payload.");
        var entity = await _documentsDb.Documents.FindAsync([payload.DocumentId], cancellationToken)
                     ?? throw new InvalidOperationException($"Document {payload.DocumentId} not found.");
        await using var stream = await _storage.GetAsync(entity.StorageKey, cancellationToken);
        var result = await _tools.WatermarkAsync(
            stream,
            new WatermarkOptions(payload.Text, Opacity: payload.Opacity),
            cancellationToken);
        await StoreResultAsync(job, result, payload.OutputFileName, "application/pdf", cancellationToken);
    }

    private async Task ExecuteProtectAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<ProtectJobPayload>(job.PayloadJson, JsonOptions)
                      ?? throw new InvalidOperationException("Invalid protect payload.");
        var entity = await _documentsDb.Documents.FindAsync([payload.DocumentId], cancellationToken)
                     ?? throw new InvalidOperationException($"Document {payload.DocumentId} not found.");
        await using var stream = await _storage.GetAsync(entity.StorageKey, cancellationToken);
        var result = await _tools.ProtectAsync(
            stream,
            new ProtectOptions(payload.UserPassword, payload.OwnerPassword),
            cancellationToken);
        await StoreResultAsync(job, result, payload.OutputFileName, "application/pdf", cancellationToken);
    }

    private async Task ExecuteExtractPagesAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<ExtractPagesJobPayload>(job.PayloadJson, JsonOptions)
                      ?? throw new InvalidOperationException("Invalid extract payload.");
        var entity = await _documentsDb.Documents.FindAsync([payload.DocumentId], cancellationToken)
                     ?? throw new InvalidOperationException($"Document {payload.DocumentId} not found.");
        await using var stream = await _storage.GetAsync(entity.StorageKey, cancellationToken);
        var result = await _tools.ExtractPagesAsync(stream, payload.PageIndexes, cancellationToken);
        await StoreResultAsync(job, result, payload.OutputFileName, "application/pdf", cancellationToken);
    }

    private async Task ExecuteReverseAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<ReverseJobPayload>(job.PayloadJson, JsonOptions)
                      ?? throw new InvalidOperationException("Invalid reverse payload.");
        var entity = await _documentsDb.Documents.FindAsync([payload.DocumentId], cancellationToken)
                     ?? throw new InvalidOperationException($"Document {payload.DocumentId} not found.");
        await using var stream = await _storage.GetAsync(entity.StorageKey, cancellationToken);
        var result = await _tools.ReversePagesAsync(stream, cancellationToken);
        await StoreResultAsync(job, result, payload.OutputFileName, "application/pdf", cancellationToken);
    }

    private async Task ExecuteDeletePagesAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<DeletePagesJobPayload>(job.PayloadJson, JsonOptions)
                      ?? throw new InvalidOperationException("Invalid delete-pages payload.");
        var entity = await _documentsDb.Documents.FindAsync([payload.DocumentId], cancellationToken)
                     ?? throw new InvalidOperationException($"Document {payload.DocumentId} not found.");
        await using var stream = await _storage.GetAsync(entity.StorageKey, cancellationToken);
        var result = await _tools.DeletePagesAsync(stream, payload.PageIndexes, cancellationToken);
        await StoreResultAsync(job, result, payload.OutputFileName, "application/pdf", cancellationToken);
    }

    private async Task ExecutePageNumbersAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<PageNumbersJobPayload>(job.PayloadJson, JsonOptions)
                      ?? throw new InvalidOperationException("Invalid page-numbers payload.");
        var entity = await _documentsDb.Documents.FindAsync([payload.DocumentId], cancellationToken)
                     ?? throw new InvalidOperationException($"Document {payload.DocumentId} not found.");
        await using var stream = await _storage.GetAsync(entity.StorageKey, cancellationToken);
        var result = await _tools.AddPageNumbersAsync(
            stream,
            new PageNumberOptions(Format: payload.Format),
            cancellationToken);
        await StoreResultAsync(job, result, payload.OutputFileName, "application/pdf", cancellationToken);
    }

    private async Task ExecuteUnlockAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<UnlockJobPayload>(job.PayloadJson, JsonOptions)
                      ?? throw new InvalidOperationException("Invalid unlock payload.");
        var entity = await _documentsDb.Documents.FindAsync([payload.DocumentId], cancellationToken)
                     ?? throw new InvalidOperationException($"Document {payload.DocumentId} not found.");
        await using var stream = await _storage.GetAsync(entity.StorageKey, cancellationToken);
        var result = await _tools.UnlockAsync(stream, payload.UserPassword, cancellationToken);
        await StoreResultAsync(job, result, payload.OutputFileName, "application/pdf", cancellationToken);
    }

    private async Task ExecutePdfToImagesAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<PdfToImagesJobPayload>(job.PayloadJson, JsonOptions)
                      ?? throw new InvalidOperationException("Invalid pdf-to-images payload.");
        var entity = await _documentsDb.Documents.FindAsync([payload.DocumentId], cancellationToken)
                     ?? throw new InvalidOperationException($"Document {payload.DocumentId} not found.");
        await using var stream = await _storage.GetAsync(entity.StorageKey, cancellationToken);
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        await using var zipMs = new MemoryStream();
        using (var zip = new ZipArchive(zipMs, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (var i = 0; i < entity.PageCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await using var pageStream = new MemoryStream(bytes);
                var rendered = await _renderer.RenderPageAsync(
                    pageStream,
                    i,
                    new RenderOptions(Dpi: payload.Dpi),
                    cancellationToken: cancellationToken);
                var entry = zip.CreateEntry($"page-{i + 1}.png", CompressionLevel.Fastest);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(rendered.ImageBytes, cancellationToken);
            }
        }

        await StoreResultAsync(job, zipMs.ToArray(), payload.OutputFileName, "application/zip", cancellationToken);
    }

    private async Task StoreResultAsync(
        ProcessingJob job,
        byte[] bytes,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        var key = $"jobs/{job.Id:N}/{fileName}";
        await using var ms = new MemoryStream(bytes);
        await _storage.EnsureBucketAsync(cancellationToken);
        await _storage.PutAsync(key, ms, contentType, cancellationToken);
        job.ResultStorageKey = key;
        job.ResultFileName = fileName;
        job.ResultContentType = contentType;
    }
}
