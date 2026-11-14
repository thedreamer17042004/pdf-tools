using Acman.Pdf.Abstractions.Models;
using Acman.Pdf.Core.Annotations;
using FluentAssertions;

namespace Acman.Pdf.Core.Tests;

public class XfdfServiceTests
{
    private readonly XfdfService _sut = new();

    [Fact]
    public void Export_Import_roundtrips_highlight()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var source = new List<PdfAnnotation>
        {
            new()
            {
                Id = id,
                Type = PdfAnnotationType.Highlight,
                PageIndex = 0,
                Rect = new PdfRect(72, 700, 120, 18),
                Color = "#FFFF00",
                Opacity = 0.35,
                Author = "tester",
                Contents = "important",
                Quads = [new PdfRect(72, 700, 120, 18)]
            }
        };

        var xml = _sut.Export(source, "hello.pdf");
        xml.Should().Contain("xfdf");
        xml.Should().Contain("highlight");
        xml.Should().Contain("hello.pdf");

        var imported = _sut.Import(xml);
        imported.Should().HaveCount(1);
        var a = imported[0];
        a.Id.Should().Be(id);
        a.Type.Should().Be(PdfAnnotationType.Highlight);
        a.PageIndex.Should().Be(0);
        a.Rect.X.Should().BeApproximately(72, 0.01);
        a.Rect.Width.Should().BeApproximately(120, 0.01);
        a.Color.Should().Be("#FFFF00");
        a.Opacity.Should().BeApproximately(0.35, 0.001);
        a.Author.Should().Be("tester");
        a.Contents.Should().Be("important");
        a.Quads.Should().NotBeNull().And.HaveCount(1);
    }

    [Fact]
    public void Import_empty_returns_empty()
    {
        _sut.Import("").Should().BeEmpty();
        _sut.Import("   ").Should().BeEmpty();
    }
}
