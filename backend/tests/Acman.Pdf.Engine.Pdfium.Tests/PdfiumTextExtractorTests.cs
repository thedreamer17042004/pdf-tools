using Acman.Pdf.Engine.Pdfium;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acman.Pdf.Engine.Pdfium.Tests;

public class PdfiumTextExtractorTests
{
    private static string SamplePath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "samples", "hello.pdf"));

    [Fact]
    public async Task Extract_returns_pages()
    {
        if (!File.Exists(SamplePath)) return;

        var sut = new PdfiumTextExtractor(NullLogger<PdfiumTextExtractor>.Instance);
        await using var stream = File.OpenRead(SamplePath);
        var result = await sut.ExtractAsync(stream);
        result.Pages.Should().NotBeEmpty();
        result.FullText.Should().NotBeNull();
    }
}
