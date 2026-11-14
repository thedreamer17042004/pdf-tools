using System.Security.Claims;
using System.Text.Json;
using Acman.BuildingBlocks.Errors;
using Acman.Pdf.Abstractions.Storage;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Modules.Documents.Infrastructure;
using Modules.Processing.Domain;
using Modules.Processing.Infrastructure;
using Modules.Processing.Messaging;

namespace Modules.Processing.Application;

public sealed record EnqueueMergeCommand(IReadOnlyList<Guid> DocumentIds, string? OutputFileName) : IRequest<ProcessingJobDto>;
public sealed record EnqueueSplitCommand(Guid DocumentId, IReadOnlyList<SplitRangeDto>? Ranges, string? OutputPrefix) : IRequest<ProcessingJobDto>;
public sealed record EnqueueRotateCommand(Guid DocumentId, int Degrees, IReadOnlyList<int>? PageIndexes, string? OutputFileName) : IRequest<ProcessingJobDto>;
public sealed record EnqueueCompressCommand(Guid DocumentId, bool RemoveMetadata, string? OutputFileName) : IRequest<ProcessingJobDto>;
public sealed record EnqueueOrganizeCommand(Guid DocumentId, IReadOnlyList<int> PageOrder, string? OutputFileName) : IRequest<ProcessingJobDto>;
public sealed record EnqueueWatermarkCommand(Guid DocumentId, string Text, double Opacity, string? OutputFileName) : IRequest<ProcessingJobDto>;
public sealed record EnqueueProtectCommand(Guid DocumentId, string UserPassword, string? OwnerPassword, string? OutputFileName) : IRequest<ProcessingJobDto>;
public sealed record EnqueueExtractPagesCommand(Guid DocumentId, IReadOnlyList<int> PageIndexes, string? OutputFileName) : IRequest<ProcessingJobDto>;
public sealed record EnqueueReverseCommand(Guid DocumentId, string? OutputFileName) : IRequest<ProcessingJobDto>;
public sealed record EnqueueDeletePagesCommand(Guid DocumentId, IReadOnlyList<int> PageIndexes, string? OutputFileName) : IRequest<ProcessingJobDto>;
public sealed record EnqueuePageNumbersCommand(Guid DocumentId, string? Format, string? OutputFileName) : IRequest<ProcessingJobDto>;
public sealed record EnqueueUnlockCommand(Guid DocumentId, string UserPassword, string? OutputFileName) : IRequest<ProcessingJobDto>;
public sealed record EnqueuePdfToImagesCommand(Guid DocumentId, int Dpi, string? OutputFileName) : IRequest<ProcessingJobDto>;
public sealed record GetJobQuery(Guid JobId) : IRequest<ProcessingJobDto>;
public sealed record DownloadJobResultQuery(Guid JobId) : IRequest<DownloadJobResult>;
public sealed record DownloadJobResult(string FileName, string ContentType, Stream Content);

public sealed class EnqueueMergeValidator : AbstractValidator<EnqueueMergeCommand>
{
    public EnqueueMergeValidator()
    {
        RuleFor(x => x.DocumentIds).NotEmpty().Must(ids => ids.Count >= 2);
    }
}

public sealed class EnqueueSplitValidator : AbstractValidator<EnqueueSplitCommand>
{
    public EnqueueSplitValidator() => RuleFor(x => x.DocumentId).NotEmpty();
}

public sealed class EnqueueRotateValidator : AbstractValidator<EnqueueRotateCommand>
{
    public EnqueueRotateValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.Degrees).Must(d => ((d % 360) + 360) % 360 is 0 or 90 or 180 or 270);
    }
}

public sealed class EnqueueCompressValidator : AbstractValidator<EnqueueCompressCommand>
{
    public EnqueueCompressValidator() => RuleFor(x => x.DocumentId).NotEmpty();
}

public sealed class EnqueueOrganizeValidator : AbstractValidator<EnqueueOrganizeCommand>
{
    public EnqueueOrganizeValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.PageOrder).NotEmpty();
    }
}

public sealed class EnqueueWatermarkValidator : AbstractValidator<EnqueueWatermarkCommand>
{
    public EnqueueWatermarkValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.Text).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Opacity).InclusiveBetween(0.05, 1);
    }
}

public sealed class EnqueueProtectValidator : AbstractValidator<EnqueueProtectCommand>
{
    public EnqueueProtectValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.UserPassword).NotEmpty().MinimumLength(4);
    }
}

public sealed class EnqueueExtractPagesValidator : AbstractValidator<EnqueueExtractPagesCommand>
{
    public EnqueueExtractPagesValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.PageIndexes).NotEmpty();
    }
}

public sealed class EnqueueReverseValidator : AbstractValidator<EnqueueReverseCommand>
{
    public EnqueueReverseValidator() => RuleFor(x => x.DocumentId).NotEmpty();
}

public sealed class EnqueueDeletePagesValidator : AbstractValidator<EnqueueDeletePagesCommand>
{
    public EnqueueDeletePagesValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.PageIndexes).NotEmpty();
    }
}

public sealed class EnqueuePageNumbersValidator : AbstractValidator<EnqueuePageNumbersCommand>
{
    public EnqueuePageNumbersValidator() => RuleFor(x => x.DocumentId).NotEmpty();
}

public sealed class EnqueueUnlockValidator : AbstractValidator<EnqueueUnlockCommand>
{
    public EnqueueUnlockValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.UserPassword).NotEmpty();
    }
}

public sealed class EnqueuePdfToImagesValidator : AbstractValidator<EnqueuePdfToImagesCommand>
{
    public EnqueuePdfToImagesValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.Dpi).InclusiveBetween(36, 300);
    }
}

public sealed class ToolCommandHandlers :
    IRequestHandler<EnqueueMergeCommand, ProcessingJobDto>,
    IRequestHandler<EnqueueSplitCommand, ProcessingJobDto>,
    IRequestHandler<EnqueueRotateCommand, ProcessingJobDto>,
    IRequestHandler<EnqueueCompressCommand, ProcessingJobDto>,
    IRequestHandler<EnqueueOrganizeCommand, ProcessingJobDto>,
    IRequestHandler<EnqueueWatermarkCommand, ProcessingJobDto>,
    IRequestHandler<EnqueueProtectCommand, ProcessingJobDto>,
    IRequestHandler<EnqueueExtractPagesCommand, ProcessingJobDto>,
    IRequestHandler<EnqueueReverseCommand, ProcessingJobDto>,
    IRequestHandler<EnqueueDeletePagesCommand, ProcessingJobDto>,
    IRequestHandler<EnqueuePageNumbersCommand, ProcessingJobDto>,
    IRequestHandler<EnqueueUnlockCommand, ProcessingJobDto>,
    IRequestHandler<EnqueuePdfToImagesCommand, ProcessingJobDto>,
    IRequestHandler<GetJobQuery, ProcessingJobDto>,
    IRequestHandler<DownloadJobResultQuery, DownloadJobResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ProcessingDbContext _db;
    private readonly DocumentsDbContext _documents;
    private readonly IJobQueue _queue;
    private readonly IJobExecutor _executor;
    private readonly IObjectStorage _storage;
    private readonly IHttpContextAccessor _http;

    public ToolCommandHandlers(
        ProcessingDbContext db,
        DocumentsDbContext documents,
        IJobQueue queue,
        IJobExecutor executor,
        IObjectStorage storage,
        IHttpContextAccessor http)
    {
        _db = db;
        _documents = documents;
        _queue = queue;
        _executor = executor;
        _storage = storage;
        _http = http;
    }

    public Task<ProcessingJobDto> Handle(EnqueueMergeCommand request, CancellationToken cancellationToken)
        => EnqueueAsync(
            ProcessingJobType.Merge,
            new MergeJobPayload
            {
                DocumentIds = request.DocumentIds.ToList(),
                OutputFileName = string.IsNullOrWhiteSpace(request.OutputFileName) ? "merged.pdf" : request.OutputFileName
            },
            request.DocumentIds,
            cancellationToken);

    public Task<ProcessingJobDto> Handle(EnqueueSplitCommand request, CancellationToken cancellationToken)
        => EnqueueAsync(
            ProcessingJobType.Split,
            new SplitJobPayload
            {
                DocumentId = request.DocumentId,
                Ranges = request.Ranges?.ToList() ?? [],
                OutputFileNamePrefix = string.IsNullOrWhiteSpace(request.OutputPrefix) ? "split" : request.OutputPrefix
            },
            [request.DocumentId],
            cancellationToken);

    public Task<ProcessingJobDto> Handle(EnqueueRotateCommand request, CancellationToken cancellationToken)
        => EnqueueAsync(
            ProcessingJobType.Rotate,
            new RotateJobPayload
            {
                DocumentId = request.DocumentId,
                Degrees = request.Degrees,
                PageIndexes = request.PageIndexes?.ToList() ?? [],
                OutputFileName = string.IsNullOrWhiteSpace(request.OutputFileName) ? "rotated.pdf" : request.OutputFileName
            },
            [request.DocumentId],
            cancellationToken);

    public Task<ProcessingJobDto> Handle(EnqueueCompressCommand request, CancellationToken cancellationToken)
        => EnqueueAsync(
            ProcessingJobType.Compress,
            new CompressJobPayload
            {
                DocumentId = request.DocumentId,
                RemoveMetadata = request.RemoveMetadata,
                OutputFileName = string.IsNullOrWhiteSpace(request.OutputFileName) ? "compressed.pdf" : request.OutputFileName
            },
            [request.DocumentId],
            cancellationToken);

    public Task<ProcessingJobDto> Handle(EnqueueOrganizeCommand request, CancellationToken cancellationToken)
        => EnqueueAsync(
            ProcessingJobType.Organize,
            new OrganizeJobPayload
            {
                DocumentId = request.DocumentId,
                PageOrder = request.PageOrder.ToList(),
                OutputFileName = string.IsNullOrWhiteSpace(request.OutputFileName) ? "organized.pdf" : request.OutputFileName
            },
            [request.DocumentId],
            cancellationToken);

    public Task<ProcessingJobDto> Handle(EnqueueWatermarkCommand request, CancellationToken cancellationToken)
        => EnqueueAsync(
            ProcessingJobType.Watermark,
            new WatermarkJobPayload
            {
                DocumentId = request.DocumentId,
                Text = request.Text,
                Opacity = request.Opacity,
                OutputFileName = string.IsNullOrWhiteSpace(request.OutputFileName) ? "watermarked.pdf" : request.OutputFileName
            },
            [request.DocumentId],
            cancellationToken);

    public Task<ProcessingJobDto> Handle(EnqueueProtectCommand request, CancellationToken cancellationToken)
        => EnqueueAsync(
            ProcessingJobType.Protect,
            new ProtectJobPayload
            {
                DocumentId = request.DocumentId,
                UserPassword = request.UserPassword,
                OwnerPassword = request.OwnerPassword,
                OutputFileName = string.IsNullOrWhiteSpace(request.OutputFileName) ? "protected.pdf" : request.OutputFileName
            },
            [request.DocumentId],
            cancellationToken);

    public Task<ProcessingJobDto> Handle(EnqueueExtractPagesCommand request, CancellationToken cancellationToken)
        => EnqueueAsync(
            ProcessingJobType.ExtractPages,
            new ExtractPagesJobPayload
            {
                DocumentId = request.DocumentId,
                PageIndexes = request.PageIndexes.ToList(),
                OutputFileName = string.IsNullOrWhiteSpace(request.OutputFileName) ? "extracted.pdf" : request.OutputFileName
            },
            [request.DocumentId],
            cancellationToken);

    public Task<ProcessingJobDto> Handle(EnqueueReverseCommand request, CancellationToken cancellationToken)
        => EnqueueAsync(
            ProcessingJobType.Reverse,
            new ReverseJobPayload
            {
                DocumentId = request.DocumentId,
                OutputFileName = string.IsNullOrWhiteSpace(request.OutputFileName) ? "reversed.pdf" : request.OutputFileName
            },
            [request.DocumentId],
            cancellationToken);

    public Task<ProcessingJobDto> Handle(EnqueueDeletePagesCommand request, CancellationToken cancellationToken)
        => EnqueueAsync(
            ProcessingJobType.DeletePages,
            new DeletePagesJobPayload
            {
                DocumentId = request.DocumentId,
                PageIndexes = request.PageIndexes.ToList(),
                OutputFileName = string.IsNullOrWhiteSpace(request.OutputFileName) ? "deleted-pages.pdf" : request.OutputFileName
            },
            [request.DocumentId],
            cancellationToken);

    public Task<ProcessingJobDto> Handle(EnqueuePageNumbersCommand request, CancellationToken cancellationToken)
        => EnqueueAsync(
            ProcessingJobType.PageNumbers,
            new PageNumbersJobPayload
            {
                DocumentId = request.DocumentId,
                Format = string.IsNullOrWhiteSpace(request.Format) ? "{n} / {total}" : request.Format,
                OutputFileName = string.IsNullOrWhiteSpace(request.OutputFileName) ? "numbered.pdf" : request.OutputFileName
            },
            [request.DocumentId],
            cancellationToken);

    public Task<ProcessingJobDto> Handle(EnqueueUnlockCommand request, CancellationToken cancellationToken)
        => EnqueueAsync(
            ProcessingJobType.Unlock,
            new UnlockJobPayload
            {
                DocumentId = request.DocumentId,
                UserPassword = request.UserPassword,
                OutputFileName = string.IsNullOrWhiteSpace(request.OutputFileName) ? "unlocked.pdf" : request.OutputFileName
            },
            [request.DocumentId],
            cancellationToken);

    public Task<ProcessingJobDto> Handle(EnqueuePdfToImagesCommand request, CancellationToken cancellationToken)
        => EnqueueAsync(
            ProcessingJobType.PdfToImages,
            new PdfToImagesJobPayload
            {
                DocumentId = request.DocumentId,
                Dpi = request.Dpi <= 0 ? 144 : request.Dpi,
                OutputFileName = string.IsNullOrWhiteSpace(request.OutputFileName) ? "pages.zip" : request.OutputFileName
            },
            [request.DocumentId],
            cancellationToken);

    public async Task<ProcessingJobDto> Handle(GetJobQuery request, CancellationToken cancellationToken)
    {
        var job = await _db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == request.JobId, cancellationToken)
                  ?? throw new NotFoundException($"Job '{request.JobId}' was not found.");
        return ToDto(job);
    }

    public async Task<DownloadJobResult> Handle(DownloadJobResultQuery request, CancellationToken cancellationToken)
    {
        var job = await _db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == request.JobId, cancellationToken)
                  ?? throw new NotFoundException($"Job '{request.JobId}' was not found.");
        if (job.Status != ProcessingJobStatus.Succeeded || string.IsNullOrWhiteSpace(job.ResultStorageKey))
        {
            throw new ValidationAppException("Job result is not ready.");
        }

        var stream = await _storage.GetAsync(job.ResultStorageKey, cancellationToken);
        return new DownloadJobResult(
            job.ResultFileName ?? "result.bin",
            job.ResultContentType ?? "application/octet-stream",
            stream);
    }

    private async Task<ProcessingJobDto> EnqueueAsync<TPayload>(
        ProcessingJobType type,
        TPayload payload,
        IEnumerable<Guid> documentIds,
        CancellationToken cancellationToken)
    {
        foreach (var id in documentIds)
        {
            var exists = await _documents.Documents.AnyAsync(d => d.Id == id, cancellationToken);
            if (!exists)
            {
                throw new NotFoundException($"Document '{id}' was not found.");
            }
        }

        var ownerId = TryGetUserId();
        var job = ProcessingJob.Create(type, JsonSerializer.Serialize(payload, JsonOptions), ownerId);
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);

        if (_queue.IsAvailable)
        {
            job.Status = ProcessingJobStatus.Queued;
            await _db.SaveChangesAsync(cancellationToken);
            await _queue.PublishAsync(job.Id, cancellationToken);
        }
        else
        {
            await _executor.ExecuteAsync(job.Id, cancellationToken);
            job = await _db.Jobs.AsNoTracking().FirstAsync(j => j.Id == job.Id, cancellationToken);
        }

        return ToDto(job);
    }

    private Guid? TryGetUserId()
    {
        var value = _http.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    internal static ProcessingJobDto ToDto(ProcessingJob job) => new(
        job.Id,
        job.Type.ToString(),
        job.Status.ToString(),
        job.Error,
        job.ResultFileName,
        job.CreatedAtUtc,
        job.CompletedAtUtc,
        !string.IsNullOrWhiteSpace(job.ResultStorageKey));
}
