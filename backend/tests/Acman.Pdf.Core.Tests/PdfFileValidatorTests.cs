using Acman.Pdf.Abstractions.Exceptions;
using Acman.Pdf.Core.Validation;
using FluentAssertions;

namespace Acman.Pdf.Core.Tests;

public class PdfFileValidatorTests
{
    [Fact]
    public void EnsurePdfMagic_accepts_pdf_header()
    {
        var act = () => PdfFileValidator.EnsurePdfMagic("%PDF-1.7"u8.ToArray());
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsurePdfMagic_rejects_non_pdf()
    {
        var act = () => PdfFileValidator.EnsurePdfMagic("notpdf"u8.ToArray());
        act.Should().Throw<PdfOpenException>();
    }

    [Fact]
    public void EnsureWithinSizeLimit_rejects_oversized()
    {
        var act = () => PdfFileValidator.EnsureWithinSizeLimit(1000, maxBytes: 100);
        act.Should().Throw<PdfOpenException>();
    }
}
