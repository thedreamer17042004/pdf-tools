using Acman.Pdf.Abstractions.Models;
using Acman.Pdf.Engine.Pdfium;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acman.Pdf.Engine.Pdfium.Tests;

public class PdfiumEngineTests
{
    private static string SamplePath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "samples", "hello.pdf"));

    [Fact]
    public async Task Open_sample_pdf_returns_page_count()
    {
        File.Exists(SamplePath).Should().BeTrue($"expected sample at {SamplePath}");
        var factory = new PdfiumDocumentFactory(NullLogger<PdfiumDocumentFactory>.Instance);

        await using var doc = await factory.OpenAsync(SamplePath);

        doc.PageCount.Should().BeGreaterThanOrEqualTo(1);
        doc.GetPages().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Render_first_page_returns_png()
    {
        var factory = new PdfiumDocumentFactory(NullLogger<PdfiumDocumentFactory>.Instance);
        var renderer = new PdfiumRenderer(factory, NullLogger<PdfiumRenderer>.Instance);

        await using var stream = File.OpenRead(SamplePath);
        var page = await renderer.RenderPageAsync(stream, 0, new RenderOptions(Dpi: 96));

        page.ContentType.Should().Be("image/png");
        page.ImageBytes.Length.Should().BeGreaterThan(100);
        page.ImageBytes[0].Should().Be(0x89);
        page.ImageBytes[1].Should().Be((byte)'P');
        page.ImageBytes[2].Should().Be((byte)'N');
        page.ImageBytes[3].Should().Be((byte)'G');
        page.WidthPixels.Should().BeGreaterThan(0);
        page.HeightPixels.Should().BeGreaterThan(0);
    }
}
