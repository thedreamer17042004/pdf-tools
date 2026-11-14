using Acman.Pdf.Abstractions.Contracts;
using Acman.Pdf.Abstractions.Exceptions;
using Acman.Pdf.Abstractions.Models;
using Docnet.Core;
using Docnet.Core.Models;
using Docnet.Core.Readers;

namespace Acman.Pdf.Engine.Pdfium;

internal sealed class PdfiumPdfDocument : IPdfDocument
{
    private readonly IDocLib _docLib;
    private readonly byte[] _bytes;
    private readonly string? _password;
    private readonly object _sync = new();
    private bool _disposed;

    public PdfiumPdfDocument(IDocLib docLib, byte[] bytes, string? password, int pageCount)
    {
        _docLib = docLib;
        _bytes = bytes;
        _password = password;
        PageCount = pageCount;
    }

    public int PageCount { get; }

    public PdfMetadata GetMetadata()
    {
        ThrowIfDisposed();
        // Docnet does not expose rich info dictionaries; return page-count-centric metadata.
        return new PdfMetadata(
            Title: null,
            Author: null,
            Subject: null,
            Keywords: null,
            Creator: "Nam-Dev PDFium Adapter",
            Producer: "Docnet/PDFium",
            CreationDate: null,
            ModificationDate: null,
            PageCount: PageCount);
    }

    public PdfPageInfo GetPage(int pageIndex)
    {
        ThrowIfDisposed();
        EnsurePageIndex(pageIndex);

        // Scaling factor 1.0 => pixel size equals PDF points at 72 DPI.
        using var reader = CreateDocReader(scalingFactor: 1.0);
        using var page = reader.GetPageReader(pageIndex);
        return new PdfPageInfo(
            Index: pageIndex,
            WidthPoints: page.GetPageWidth(),
            HeightPoints: page.GetPageHeight(),
            RotationDegrees: 0);
    }

    public IReadOnlyList<PdfPageInfo> GetPages()
    {
        ThrowIfDisposed();
        var pages = new List<PdfPageInfo>(PageCount);
        for (var i = 0; i < PageCount; i++)
        {
            pages.Add(GetPage(i));
        }

        return pages;
    }

    internal IDocReader CreateDocReader(double scalingFactor)
    {
        ThrowIfDisposed();
        lock (_sync)
        {
            var dim = new PageDimensions(scalingFactor);
            return string.IsNullOrEmpty(_password)
                ? _docLib.GetDocReader(_bytes, dim)
                : _docLib.GetDocReader(_bytes, _password, dim);
        }
    }

    internal void EnsurePageIndex(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= PageCount)
        {
            throw new PdfPageOutOfRangeException(pageIndex, PageCount);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        _disposed = true;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
