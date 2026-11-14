using Acman.Pdf.Abstractions.Contracts;
using Acman.Pdf.Abstractions.Exceptions;
using Acman.Pdf.Core.Validation;
using Microsoft.Extensions.Logging;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Security;

namespace Acman.Pdf.Engine.PdfSharp;

/// <summary>
/// PDF manipulation via PDFsharp 6.2.x (MIT). Rendering remains on the PDFium adapter.
/// </summary>
public sealed class PdfSharpManipulationService : IPdfManipulationService
{
    private readonly ILogger<PdfSharpManipulationService> _logger;

    public PdfSharpManipulationService(ILogger<PdfSharpManipulationService> logger)
    {
        _logger = logger;
        EnsureFonts();
    }

    private static void EnsureFonts()
    {
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        }
    }

    public async Task<byte[]> MergeAsync(IReadOnlyList<Stream> sources, CancellationToken cancellationToken = default)
    {
        if (sources.Count == 0)
        {
            throw new PdfOpenException("At least one PDF is required to merge.");
        }

        try
        {
            using var output = new PdfDocument();
            foreach (var source in sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var bytes = await ReadAllAsync(source, cancellationToken);
                PdfFileValidator.EnsurePdfMagic(bytes);
                using var input = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
                for (var i = 0; i < input.PageCount; i++)
                {
                    output.AddPage(input.Pages[i]);
                }
            }

            _logger.LogInformation("Merged {Count} PDF(s) into {Pages} page(s)", sources.Count, output.PageCount);
            return SaveToBytes(output);
        }
        catch (PdfException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfOpenException("Failed to merge PDF documents.", ex);
        }
    }

    public async Task<IReadOnlyList<byte[]>> SplitAsync(
        Stream source,
        IReadOnlyList<PageRange>? ranges = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = await ReadAllAsync(source, cancellationToken);
            PdfFileValidator.EnsurePdfMagic(bytes);
            using var input = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);

            var effectiveRanges = ranges is { Count: > 0 }
                ? ranges
                : Enumerable.Range(1, input.PageCount).Select(i => new PageRange(i, i)).ToList();

            var results = new List<byte[]>(effectiveRanges.Count);
            foreach (var range in effectiveRanges)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (range.From < 1 || range.To > input.PageCount || range.From > range.To)
                {
                    throw new PdfPageOutOfRangeException(range.From - 1, input.PageCount);
                }

                using var part = new PdfDocument();
                for (var page = range.From; page <= range.To; page++)
                {
                    part.AddPage(input.Pages[page - 1]);
                }

                results.Add(SaveToBytes(part));
            }

            _logger.LogInformation("Split PDF into {Count} part(s)", results.Count);
            return results;
        }
        catch (PdfException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfOpenException("Failed to split PDF document.", ex);
        }
    }

    public async Task<byte[]> RotateAsync(
        Stream source,
        int degreesClockwise,
        IReadOnlyList<int>? pageIndexes = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = ((degreesClockwise % 360) + 360) % 360;
        if (normalized is not (0 or 90 or 180 or 270))
        {
            throw new PdfOpenException("Rotation must be a multiple of 90 degrees.");
        }

        try
        {
            var bytes = await ReadAllAsync(source, cancellationToken);
            PdfFileValidator.EnsurePdfMagic(bytes);
            using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Modify);

            var targets = pageIndexes is { Count: > 0 }
                ? pageIndexes
                : Enumerable.Range(0, doc.PageCount).ToList();

            foreach (var index in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (index < 0 || index >= doc.PageCount)
                {
                    throw new PdfPageOutOfRangeException(index, doc.PageCount);
                }

                var page = doc.Pages[index];
                page.Rotate = (page.Rotate + normalized) % 360;
            }

            _logger.LogInformation("Rotated {Count} page(s) by {Degrees}°", targets.Count, normalized);
            return SaveToBytes(doc);
        }
        catch (PdfException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfOpenException("Failed to rotate PDF pages.", ex);
        }
    }

    public async Task<CompressResult> CompressAsync(
        Stream source,
        CompressOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CompressOptions();
        try
        {
            var original = await ReadAllAsync(source, cancellationToken);
            PdfFileValidator.EnsurePdfMagic(original);

            byte[] compressed;
            if (options.RebuildViaImport)
            {
                using var input = PdfReader.Open(new MemoryStream(original), PdfDocumentOpenMode.Import);
                using var output = new PdfDocument();
                if (options.RemoveMetadata)
                {
                    output.Info.Title = string.Empty;
                    output.Info.Author = string.Empty;
                    output.Info.Subject = string.Empty;
                    output.Info.Keywords = string.Empty;
                    output.Info.Creator = "Nam-Dev";
                }

                for (var i = 0; i < input.PageCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    output.AddPage(input.Pages[i]);
                }

                output.Options.CompressContentStreams = true;
                compressed = SaveToBytes(output);
            }
            else
            {
                using var doc = PdfReader.Open(new MemoryStream(original), PdfDocumentOpenMode.Modify);
                if (options.RemoveMetadata)
                {
                    doc.Info.Title = string.Empty;
                    doc.Info.Author = string.Empty;
                    doc.Info.Subject = string.Empty;
                    doc.Info.Keywords = string.Empty;
                }

                doc.Options.CompressContentStreams = true;
                compressed = SaveToBytes(doc);
            }

            // Never grow the file for "compress".
            if (compressed.Length >= original.Length)
            {
                compressed = original;
            }

            _logger.LogInformation(
                "Compressed PDF {Original} -> {Compressed} bytes ({Ratio:P1})",
                original.Length, compressed.Length, (double)compressed.Length / Math.Max(1, original.Length));

            return new CompressResult(compressed, original.Length, compressed.Length);
        }
        catch (PdfException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfOpenException("Failed to compress PDF.", ex);
        }
    }

    public async Task<byte[]> OrganizeAsync(
        Stream source,
        IReadOnlyList<int> pageOrder,
        CancellationToken cancellationToken = default)
    {
        if (pageOrder.Count == 0)
        {
            throw new PdfOpenException("Organize requires at least one page index.");
        }

        try
        {
            var bytes = await ReadAllAsync(source, cancellationToken);
            PdfFileValidator.EnsurePdfMagic(bytes);
            using var input = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
            using var output = new PdfDocument();

            foreach (var index in pageOrder)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (index < 0 || index >= input.PageCount)
                {
                    throw new PdfPageOutOfRangeException(index, input.PageCount);
                }

                output.AddPage(input.Pages[index]);
            }

            _logger.LogInformation("Organized PDF into {Count} page(s)", output.PageCount);
            return SaveToBytes(output);
        }
        catch (PdfException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfOpenException("Failed to organize PDF pages.", ex);
        }
    }

    public async Task<byte[]> WatermarkAsync(
        Stream source,
        WatermarkOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureFonts();
            var bytes = await ReadAllAsync(source, cancellationToken);
            PdfFileValidator.EnsurePdfMagic(bytes);
            using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Modify);
            var brush = new XSolidBrush(XColor.FromArgb(
                (int)(Math.Clamp(options.Opacity, 0.05, 1) * 255),
                ParseHex(options.ColorHex)));
            var font = new XFont("Arial", options.FontSize, XFontStyleEx.Bold);

            for (var i = 0; i < doc.PageCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = doc.Pages[i];
                using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                var size = gfx.MeasureString(options.Text, font);
                gfx.TranslateTransform(page.Width.Point / 2, page.Height.Point / 2);
                gfx.RotateTransform(options.AngleDegrees);
                gfx.DrawString(
                    options.Text,
                    font,
                    brush,
                    new XPoint(-size.Width / 2, -size.Height / 2));
            }

            _logger.LogInformation("Applied watermark to {Count} page(s)", doc.PageCount);
            return SaveToBytes(doc);
        }
        catch (PdfException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfOpenException("Failed to watermark PDF.", ex);
        }
    }

    public async Task<byte[]> ProtectAsync(
        Stream source,
        ProtectOptions options,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.UserPassword))
        {
            throw new PdfOpenException("User password is required to protect a PDF.");
        }

        try
        {
            var bytes = await ReadAllAsync(source, cancellationToken);
            PdfFileValidator.EnsurePdfMagic(bytes);
            using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Modify);
            var security = doc.SecuritySettings;
            security.UserPassword = options.UserPassword;
            security.OwnerPassword = string.IsNullOrWhiteSpace(options.OwnerPassword)
                ? options.UserPassword
                : options.OwnerPassword;
            security.PermitPrint = true;
            security.PermitModifyDocument = false;
            security.PermitExtractContent = false;

            _logger.LogInformation("Protected PDF with user password");
            return SaveToBytes(doc);
        }
        catch (PdfException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfOpenException("Failed to password-protect PDF.", ex);
        }
    }

    public async Task<byte[]> ImagesToPdfAsync(
        IReadOnlyList<ImagePageInput> images,
        CancellationToken cancellationToken = default)
    {
        if (images.Count == 0)
        {
            throw new PdfOpenException("At least one image is required.");
        }

        try
        {
            using var output = new PdfDocument();
            foreach (var image in images)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await using var imgStream = new MemoryStream(image.Bytes);
                using var xImage = XImage.FromStream(imgStream);
                var page = output.AddPage();
                // PDF points ≈ 72 DPI; keep aspect ratio fitting letter-ish max.
                var maxW = 612.0;
                var maxH = 792.0;
                var scale = Math.Min(maxW / xImage.PixelWidth, maxH / xImage.PixelHeight);
                // XImage size is in points already for some formats; use PointWidth when available.
                var drawW = xImage.PointWidth > 0 ? Math.Min(xImage.PointWidth, maxW) : xImage.PixelWidth * scale;
                var drawH = xImage.PointHeight > 0 ? Math.Min(xImage.PointHeight, maxH) : xImage.PixelHeight * scale;
                if (xImage.PointWidth <= 0)
                {
                    drawW = xImage.PixelWidth * 72.0 / 96.0;
                    drawH = xImage.PixelHeight * 72.0 / 96.0;
                    var fit = Math.Min(maxW / drawW, maxH / drawH);
                    drawW *= fit;
                    drawH *= fit;
                }

                page.Width = XUnit.FromPoint(drawW);
                page.Height = XUnit.FromPoint(drawH);
                using var gfx = XGraphics.FromPdfPage(page);
                gfx.DrawImage(xImage, 0, 0, drawW, drawH);
            }

            _logger.LogInformation("Created PDF from {Count} image(s)", images.Count);
            return SaveToBytes(output);
        }
        catch (PdfException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfOpenException("Failed to convert images to PDF.", ex);
        }
    }

    public async Task<byte[]> ExtractPagesAsync(
        Stream source,
        IReadOnlyList<int> pageIndexes,
        CancellationToken cancellationToken = default)
    {
        if (pageIndexes.Count == 0)
        {
            throw new PdfOpenException("Extract requires at least one page index.");
        }

        return await OrganizeAsync(source, pageIndexes, cancellationToken);
    }

    public async Task<byte[]> ReversePagesAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        var bytes = await ReadAllAsync(source, cancellationToken);
        PdfFileValidator.EnsurePdfMagic(bytes);
        using var input = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        var order = Enumerable.Range(0, input.PageCount).Reverse().ToList();
        await using var again = new MemoryStream(bytes);
        return await OrganizeAsync(again, order, cancellationToken);
    }

    public async Task<byte[]> DeletePagesAsync(
        Stream source,
        IReadOnlyList<int> pageIndexesToDelete,
        CancellationToken cancellationToken = default)
    {
        if (pageIndexesToDelete.Count == 0)
        {
            throw new PdfOpenException("Delete requires at least one page index.");
        }

        var bytes = await ReadAllAsync(source, cancellationToken);
        PdfFileValidator.EnsurePdfMagic(bytes);
        using var input = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        var remove = pageIndexesToDelete.ToHashSet();
        var keep = Enumerable.Range(0, input.PageCount).Where(i => !remove.Contains(i)).ToList();
        if (keep.Count == 0)
        {
            throw new PdfOpenException("Cannot delete all pages from a PDF.");
        }

        await using var again = new MemoryStream(bytes);
        return await OrganizeAsync(again, keep, cancellationToken);
    }

    public async Task<byte[]> AddPageNumbersAsync(
        Stream source,
        PageNumberOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new PageNumberOptions();
        try
        {
            EnsureFonts();
            var bytes = await ReadAllAsync(source, cancellationToken);
            PdfFileValidator.EnsurePdfMagic(bytes);
            using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Modify);
            var font = new XFont("Arial", options.FontSize, XFontStyleEx.Regular);
            var brush = new XSolidBrush(ParseHex(options.ColorHex));
            var total = doc.PageCount;

            for (var i = 0; i < total; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = doc.Pages[i];
                var label = options.Format
                    .Replace("{n}", (i + 1).ToString(), StringComparison.Ordinal)
                    .Replace("{total}", total.ToString(), StringComparison.Ordinal);
                using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                var size = gfx.MeasureString(label, font);
                var margin = options.MarginPoints;
                var x = options.Position switch
                {
                    PageNumberPosition.BottomLeft => margin,
                    PageNumberPosition.BottomRight => page.Width.Point - size.Width - margin,
                    _ => (page.Width.Point - size.Width) / 2
                };
                // PdfSharp page graphics: Y grows upward from bottom-left.
                var y = options.Position == PageNumberPosition.TopCenter
                    ? page.Height.Point - margin - size.Height
                    : margin;

                gfx.DrawString(label, font, brush, new XPoint(x, y));
            }

            _logger.LogInformation("Added page numbers to {Count} page(s)", total);
            return SaveToBytes(doc);
        }
        catch (PdfException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfOpenException("Failed to add page numbers.", ex);
        }
    }

    public async Task<byte[]> UnlockAsync(
        Stream source,
        string userPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userPassword))
        {
            throw new PdfOpenException("Password is required to unlock a PDF.");
        }

        try
        {
            var bytes = await ReadAllAsync(source, cancellationToken);
            PdfFileValidator.EnsurePdfMagic(bytes);
            using var input = PdfReader.Open(new MemoryStream(bytes), userPassword, PdfDocumentOpenMode.Import);
            using var output = new PdfDocument();
            for (var i = 0; i < input.PageCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                output.AddPage(input.Pages[i]);
            }

            _logger.LogInformation("Unlocked PDF into {Count} page(s)", output.PageCount);
            return SaveToBytes(output);
        }
        catch (PdfException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfOpenException("Failed to unlock PDF. Check the password.", ex);
        }
    }

    public async Task<byte[]> InsertBlankPagesAsync(
        Stream source,
        int atIndex,
        int count = 1,
        double? widthPoints = null,
        double? heightPoints = null,
        CancellationToken cancellationToken = default)
    {
        if (count < 1) throw new PdfOpenException("Blank page count must be >= 1.");

        try
        {
            var bytes = await ReadAllAsync(source, cancellationToken);
            PdfFileValidator.EnsurePdfMagic(bytes);
            using var input = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
            if (atIndex < 0 || atIndex > input.PageCount)
            {
                throw new PdfPageOutOfRangeException(atIndex, input.PageCount);
            }

            var w = widthPoints ?? (input.PageCount > 0 ? input.Pages[0].Width.Point : 612);
            var h = heightPoints ?? (input.PageCount > 0 ? input.Pages[0].Height.Point : 792);
            using var output = new PdfDocument();
            for (var i = 0; i < atIndex; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                output.AddPage(input.Pages[i]);
            }

            for (var i = 0; i < count; i++)
            {
                var blank = output.AddPage();
                blank.Width = XUnit.FromPoint(w);
                blank.Height = XUnit.FromPoint(h);
            }

            for (var i = atIndex; i < input.PageCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                output.AddPage(input.Pages[i]);
            }

            _logger.LogInformation("Inserted {Count} blank page(s) at {Index}", count, atIndex);
            return SaveToBytes(output);
        }
        catch (PdfException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfOpenException("Failed to insert blank pages.", ex);
        }
    }

    public async Task<byte[]> InsertPagesAsync(
        Stream source,
        Stream pagesSource,
        int atIndex,
        IReadOnlyList<int>? pageIndexes = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var baseBytes = await ReadAllAsync(source, cancellationToken);
            var insertBytes = await ReadAllAsync(pagesSource, cancellationToken);
            PdfFileValidator.EnsurePdfMagic(baseBytes);
            PdfFileValidator.EnsurePdfMagic(insertBytes);
            using var input = PdfReader.Open(new MemoryStream(baseBytes), PdfDocumentOpenMode.Import);
            using var insert = PdfReader.Open(new MemoryStream(insertBytes), PdfDocumentOpenMode.Import);
            if (atIndex < 0 || atIndex > input.PageCount)
            {
                throw new PdfPageOutOfRangeException(atIndex, input.PageCount);
            }

            var indexes = pageIndexes is { Count: > 0 }
                ? pageIndexes
                : Enumerable.Range(0, insert.PageCount).ToList();

            using var output = new PdfDocument();
            for (var i = 0; i < atIndex; i++)
            {
                output.AddPage(input.Pages[i]);
            }

            foreach (var index in indexes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (index < 0 || index >= insert.PageCount)
                {
                    throw new PdfPageOutOfRangeException(index, insert.PageCount);
                }

                output.AddPage(insert.Pages[index]);
            }

            for (var i = atIndex; i < input.PageCount; i++)
            {
                output.AddPage(input.Pages[i]);
            }

            _logger.LogInformation("Inserted {Count} page(s) at {Index}", indexes.Count, atIndex);
            return SaveToBytes(output);
        }
        catch (PdfException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfOpenException("Failed to insert pages.", ex);
        }
    }

    public async Task<byte[]> AppendAsync(
        Stream source,
        Stream appendix,
        CancellationToken cancellationToken = default)
    {
        var baseBytes = await ReadAllAsync(source, cancellationToken);
        PdfFileValidator.EnsurePdfMagic(baseBytes);
        using var input = PdfReader.Open(new MemoryStream(baseBytes), PdfDocumentOpenMode.Import);
        await using var again = new MemoryStream(baseBytes);
        return await InsertPagesAsync(again, appendix, input.PageCount, null, cancellationToken);
    }

    public async Task<byte[]> AddTextAsync(
        Stream source,
        AddTextOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureFonts();
            var bytes = await ReadAllAsync(source, cancellationToken);
            PdfFileValidator.EnsurePdfMagic(bytes);
            using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Modify);
            var font = new XFont("Arial", options.FontSize, XFontStyleEx.Regular);
            var brush = new XSolidBrush(ParseHex(options.ColorHex));
            var targets = options.PageIndex is int only
                ? [only]
                : Enumerable.Range(0, doc.PageCount).ToList();

            foreach (var index in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (index < 0 || index >= doc.PageCount)
                {
                    throw new PdfPageOutOfRangeException(index, doc.PageCount);
                }

                using var gfx = XGraphics.FromPdfPage(doc.Pages[index], XGraphicsPdfPageOptions.Append);
                gfx.DrawString(options.Text, font, brush, new XPoint(options.XPoints, options.YPoints));
            }

            _logger.LogInformation("Added text overlay on {Count} page(s)", targets.Count);
            return SaveToBytes(doc);
        }
        catch (PdfException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfOpenException("Failed to add text.", ex);
        }
    }

    public async Task<byte[]> HeaderFooterAsync(
        Stream source,
        HeaderFooterOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureFonts();
            var bytes = await ReadAllAsync(source, cancellationToken);
            PdfFileValidator.EnsurePdfMagic(bytes);
            using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Modify);
            var font = new XFont("Arial", options.FontSize, XFontStyleEx.Regular);
            var brush = new XSolidBrush(ParseHex(options.ColorHex));

            for (var i = 0; i < doc.PageCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = doc.Pages[i];
                using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                if (!string.IsNullOrWhiteSpace(options.Header))
                {
                    var size = gfx.MeasureString(options.Header, font);
                    gfx.DrawString(
                        options.Header,
                        font,
                        brush,
                        new XPoint((page.Width.Point - size.Width) / 2, page.Height.Point - options.MarginPoints - size.Height));
                }

                if (!string.IsNullOrWhiteSpace(options.Footer))
                {
                    var size = gfx.MeasureString(options.Footer, font);
                    gfx.DrawString(
                        options.Footer,
                        font,
                        brush,
                        new XPoint((page.Width.Point - size.Width) / 2, options.MarginPoints));
                }
            }

            _logger.LogInformation("Applied header/footer to {Count} page(s)", doc.PageCount);
            return SaveToBytes(doc);
        }
        catch (PdfException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfOpenException("Failed to apply header/footer.", ex);
        }
    }

    public async Task<byte[]> StampImageAsync(
        Stream source,
        StampImageOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = await ReadAllAsync(source, cancellationToken);
            PdfFileValidator.EnsurePdfMagic(bytes);
            using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Modify);
            await using var imgStream = new MemoryStream(options.ImageBytes);
            using var xImage = XImage.FromStream(imgStream);
            var targets = options.PageIndex is int only
                ? [only]
                : Enumerable.Range(0, doc.PageCount).ToList();

            foreach (var index in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (index < 0 || index >= doc.PageCount)
                {
                    throw new PdfPageOutOfRangeException(index, doc.PageCount);
                }

                using var gfx = XGraphics.FromPdfPage(doc.Pages[index], XGraphicsPdfPageOptions.Append);
                if (options.Opacity < 0.999)
                {
                    gfx.Save();
                    // PdfSharp opacity via color transform is limited; draw at full then note best-effort.
                }

                gfx.DrawImage(xImage, options.XPoints, options.YPoints, options.WidthPoints, options.HeightPoints);
            }

            _logger.LogInformation("Stamped image on {Count} page(s)", targets.Count);
            return SaveToBytes(doc);
        }
        catch (PdfException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfOpenException("Failed to stamp image.", ex);
        }
    }

    public async Task<byte[]> RedactCoverAsync(
        Stream source,
        IReadOnlyList<RedactRegion> regions,
        CancellationToken cancellationToken = default)
    {
        if (regions.Count == 0)
        {
            throw new PdfOpenException("At least one redact region is required.");
        }

        try
        {
            var bytes = await ReadAllAsync(source, cancellationToken);
            PdfFileValidator.EnsurePdfMagic(bytes);
            using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Modify);

            foreach (var region in regions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (region.PageIndex < 0 || region.PageIndex >= doc.PageCount)
                {
                    throw new PdfPageOutOfRangeException(region.PageIndex, doc.PageCount);
                }

                using var gfx = XGraphics.FromPdfPage(doc.Pages[region.PageIndex], XGraphicsPdfPageOptions.Append);
                var brush = new XSolidBrush(ParseHex(region.ColorHex));
                gfx.DrawRectangle(brush, region.XPoints, region.YPoints, region.WidthPoints, region.HeightPoints);
            }

            _logger.LogInformation("Covered {Count} redact region(s)", regions.Count);
            return SaveToBytes(doc);
        }
        catch (PdfException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfOpenException("Failed to redact/cover regions.", ex);
        }
    }

    public async Task<byte[]> CropPagesAsync(
        Stream source,
        CropOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = await ReadAllAsync(source, cancellationToken);
            PdfFileValidator.EnsurePdfMagic(bytes);
            using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Modify);
            var targets = options.PageIndexes is { Count: > 0 }
                ? options.PageIndexes
                : Enumerable.Range(0, doc.PageCount).ToList();

            var box = new PdfRectangle(new XRect(
                options.LeftPoints,
                options.BottomPoints,
                Math.Max(0, options.RightPoints - options.LeftPoints),
                Math.Max(0, options.TopPoints - options.BottomPoints)));

            foreach (var index in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (index < 0 || index >= doc.PageCount)
                {
                    throw new PdfPageOutOfRangeException(index, doc.PageCount);
                }

                var page = doc.Pages[index];
                page.MediaBox = box;
                page.CropBox = box;
            }

            _logger.LogInformation("Cropped {Count} page(s)", targets.Count);
            return SaveToBytes(doc);
        }
        catch (PdfException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfOpenException("Failed to crop pages.", ex);
        }
    }

    public async Task<byte[]> RepairAsync(Stream source, CancellationToken cancellationToken = default)
    {
        try
        {
            var original = await ReadAllAsync(source, cancellationToken);
            PdfFileValidator.EnsurePdfMagic(original);
            using var input = PdfReader.Open(new MemoryStream(original), PdfDocumentOpenMode.Import);
            using var output = new PdfDocument();
            output.Info.Creator = "Nam-Dev Repair";
            for (var i = 0; i < input.PageCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                output.AddPage(input.Pages[i]);
            }

            output.Options.CompressContentStreams = true;
            _logger.LogInformation("Repaired PDF into {Count} page(s)", output.PageCount);
            return SaveToBytes(output);
        }
        catch (PdfException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfOpenException("Failed to repair PDF.", ex);
        }
    }

    public async Task<byte[]> SignStampAsync(
        Stream source,
        SignStampOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureFonts();
            var bytes = await ReadAllAsync(source, cancellationToken);
            PdfFileValidator.EnsurePdfMagic(bytes);
            using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Modify);
            if (options.PageIndex < 0 || options.PageIndex >= doc.PageCount)
            {
                throw new PdfPageOutOfRangeException(options.PageIndex, doc.PageCount);
            }

            var page = doc.Pages[options.PageIndex];
            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            var pen = new XPen(ParseHex(options.ColorHex), 1.2);
            var brush = new XSolidBrush(ParseHex(options.ColorHex));
            var rect = new XRect(options.XPoints, options.YPoints, options.WidthPoints, options.HeightPoints);
            gfx.DrawRectangle(pen, rect);

            if (options.ImageBytes is { Length: > 0 })
            {
                await using var imgStream = new MemoryStream(options.ImageBytes);
                using var xImage = XImage.FromStream(imgStream);
                var pad = 4.0;
                gfx.DrawImage(
                    xImage,
                    options.XPoints + pad,
                    options.YPoints + pad,
                    options.WidthPoints - pad * 2,
                    options.HeightPoints - pad * 2);
            }

            if (!string.IsNullOrWhiteSpace(options.SignerName))
            {
                var font = new XFont("Arial", options.FontSize, XFontStyleEx.Italic);
                var label = options.SignerName!;
                var size = gfx.MeasureString(label, font);
                gfx.DrawString(
                    label,
                    font,
                    brush,
                    new XPoint(
                        options.XPoints + (options.WidthPoints - size.Width) / 2,
                        options.YPoints + (options.HeightPoints - size.Height) / 2));
            }

            var metaFont = new XFont("Arial", 7, XFontStyleEx.Regular);
            gfx.DrawString(
                $"Signed {DateTime.UtcNow:yyyy-MM-dd}",
                metaFont,
                brush,
                new XPoint(options.XPoints + 4, options.YPoints + 4));

            _logger.LogInformation("Applied visual signature stamp on page {Page}", options.PageIndex);
            return SaveToBytes(doc);
        }
        catch (PdfException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfOpenException("Failed to apply signature stamp.", ex);
        }
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        await using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    private static byte[] SaveToBytes(PdfDocument document)
    {
        using var ms = new MemoryStream();
        document.Save(ms, false);
        return ms.ToArray();
    }

    private static XColor ParseHex(string hex)
    {
        var clean = hex.Trim().TrimStart('#');
        if (clean.Length == 3)
        {
            clean = string.Concat(clean.Select(c => $"{c}{c}"));
        }

        if (clean.Length < 6)
        {
            return XColors.Gray;
        }

        var r = Convert.ToByte(clean[..2], 16);
        var g = Convert.ToByte(clean.Substring(2, 2), 16);
        var b = Convert.ToByte(clean.Substring(4, 2), 16);
        return XColor.FromArgb(r, g, b);
    }
}
