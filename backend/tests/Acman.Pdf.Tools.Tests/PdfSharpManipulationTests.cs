using Acman.Pdf.Abstractions.Contracts;
using Acman.Pdf.Engine.PdfSharp;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acman.Pdf.Tools.Tests;

public class PdfSharpManipulationTests
{
    private static string SamplePath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "samples", "hello.pdf"));

    private readonly IPdfManipulationService _tools =
        new PdfSharpManipulationService(NullLogger<PdfSharpManipulationService>.Instance);

    [Fact]
    public async Task Merge_two_copies_doubles_page_count()
    {
        await using var a = File.OpenRead(SamplePath);
        await using var b = File.OpenRead(SamplePath);
        var merged = await _tools.MergeAsync([a, b]);

        merged.Should().NotBeEmpty();
        System.Text.Encoding.ASCII.GetString(merged, 0, 4).Should().Be("%PDF");

        await using var stream = new MemoryStream(merged);
        var parts = await _tools.SplitAsync(stream);
        parts.Count.Should().Be(4); // hello.pdf has 2 pages × 2
    }

    [Fact]
    public async Task Split_produces_one_file_per_page()
    {
        await using var stream = File.OpenRead(SamplePath);
        var parts = await _tools.SplitAsync(stream);
        parts.Count.Should().BeGreaterThanOrEqualTo(1);
        foreach (var part in parts)
        {
            System.Text.Encoding.ASCII.GetString(part, 0, 4).Should().Be("%PDF");
        }
    }

    [Fact]
    public async Task Rotate_90_returns_valid_pdf()
    {
        await using var stream = File.OpenRead(SamplePath);
        var rotated = await _tools.RotateAsync(stream, 90);
        System.Text.Encoding.ASCII.GetString(rotated, 0, 4).Should().Be("%PDF");
        rotated.Length.Should().BeGreaterThan(100);
    }

    [Fact]
    public async Task Compress_returns_valid_pdf()
    {
        await using var stream = File.OpenRead(SamplePath);
        var result = await _tools.CompressAsync(stream, new CompressOptions(RemoveMetadata: true));
        System.Text.Encoding.ASCII.GetString(result.Bytes, 0, 4).Should().Be("%PDF");
        result.OriginalSizeBytes.Should().BeGreaterThan(0);
        result.CompressedSizeBytes.Should().BeGreaterThan(100);
    }

    [Fact]
    public async Task Organize_reorders_pages()
    {
        await using var stream = File.OpenRead(SamplePath);
        var organized = await _tools.OrganizeAsync(stream, [1, 0]);
        System.Text.Encoding.ASCII.GetString(organized, 0, 4).Should().Be("%PDF");

        await using var check = new MemoryStream(organized);
        var parts = await _tools.SplitAsync(check);
        parts.Count.Should().Be(2);
    }

    [Fact]
    public async Task Watermark_returns_valid_pdf()
    {
        await using var stream = File.OpenRead(SamplePath);
        var marked = await _tools.WatermarkAsync(stream, new WatermarkOptions("Nam-Dev", Opacity: 0.3));
        System.Text.Encoding.ASCII.GetString(marked, 0, 4).Should().Be("%PDF");
        marked.Length.Should().BeGreaterThan(100);
    }

    [Fact]
    public async Task Protect_returns_valid_pdf()
    {
        await using var stream = File.OpenRead(SamplePath);
        var protectedPdf = await _tools.ProtectAsync(stream, new ProtectOptions("secret1"));
        System.Text.Encoding.ASCII.GetString(protectedPdf, 0, 4).Should().Be("%PDF");
        protectedPdf.Length.Should().BeGreaterThan(100);
    }

    [Fact]
    public async Task Reverse_and_extract_and_page_numbers()
    {
        await using var stream = File.OpenRead(SamplePath);
        var reversed = await _tools.ReversePagesAsync(stream);
        System.Text.Encoding.ASCII.GetString(reversed, 0, 4).Should().Be("%PDF");

        await using var extractIn = new MemoryStream(reversed);
        var extracted = await _tools.ExtractPagesAsync(extractIn, [0]);
        System.Text.Encoding.ASCII.GetString(extracted, 0, 4).Should().Be("%PDF");

        await using var numIn = File.OpenRead(SamplePath);
        var numbered = await _tools.AddPageNumbersAsync(numIn);
        System.Text.Encoding.ASCII.GetString(numbered, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task Delete_pages_keeps_remaining()
    {
        await using var stream = File.OpenRead(SamplePath);
        var deleted = await _tools.DeletePagesAsync(stream, [0]);
        System.Text.Encoding.ASCII.GetString(deleted, 0, 4).Should().Be("%PDF");
        await using var check = new MemoryStream(deleted);
        var parts = await _tools.SplitAsync(check);
        parts.Count.Should().Be(1);
    }

    [Fact]
    public async Task Protect_then_unlock_roundtrip()
    {
        await using var stream = File.OpenRead(SamplePath);
        var locked = await _tools.ProtectAsync(stream, new ProtectOptions("secret1"));
        await using var lockedStream = new MemoryStream(locked);
        var unlocked = await _tools.UnlockAsync(lockedStream, "secret1");
        System.Text.Encoding.ASCII.GetString(unlocked, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task Insert_blank_and_header_footer_and_add_text()
    {
        await using var stream = File.OpenRead(SamplePath);
        var withBlank = await _tools.InsertBlankPagesAsync(stream, 1, 1);
        System.Text.Encoding.ASCII.GetString(withBlank, 0, 4).Should().Be("%PDF");

        await using var hfIn = new MemoryStream(withBlank);
        var hf = await _tools.HeaderFooterAsync(hfIn, new HeaderFooterOptions("Nam-Dev", "confidential"));
        System.Text.Encoding.ASCII.GetString(hf, 0, 4).Should().Be("%PDF");

        await using var textIn = new MemoryStream(hf);
        var texted = await _tools.AddTextAsync(textIn, new AddTextOptions("Hello", 72, 720, PageIndex: 0));
        System.Text.Encoding.ASCII.GetString(texted, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task Info_and_bookmarks_roundtrip()
    {
        var infoSvc = new PdfSharpDocumentInfoService(NullLogger<PdfSharpDocumentInfoService>.Instance);
        var bookmarkSvc = new PdfSharpBookmarkService(NullLogger<PdfSharpBookmarkService>.Instance);

        await using var stream = File.OpenRead(SamplePath);
        var info = await infoSvc.GetInfoAsync(stream);
        info.PageCount.Should().BeGreaterThan(0);

        await using var setIn = File.OpenRead(SamplePath);
        var updated = await infoSvc.SetInfoAsync(setIn, new PdfInfoUpdate(Title: "Core Test", Author: "Nam-Dev"));
        System.Text.Encoding.ASCII.GetString(updated, 0, 4).Should().Be("%PDF");

        await using var bmIn = new MemoryStream(updated);
        var withBm = await bookmarkSvc.SetAsync(bmIn, [new PdfBookmarkInput("Cover", 0)]);
        await using var listIn = new MemoryStream(withBm);
        var bookmarks = await bookmarkSvc.ListAsync(listIn);
        bookmarks.Should().ContainSingle(b => b.Title == "Cover");
    }

    [Fact]
    public async Task Redact_cover_returns_valid_pdf()
    {
        await using var stream = File.OpenRead(SamplePath);
        var redacted = await _tools.RedactCoverAsync(stream, [
            new RedactRegion(0, 50, 50, 100, 40)
        ]);
        System.Text.Encoding.ASCII.GetString(redacted, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task Repair_and_sign_stamp_return_valid_pdf()
    {
        await using var stream = File.OpenRead(SamplePath);
        var repaired = await _tools.RepairAsync(stream);
        System.Text.Encoding.ASCII.GetString(repaired, 0, 4).Should().Be("%PDF");

        await using var signIn = new MemoryStream(repaired);
        var signed = await _tools.SignStampAsync(signIn, new SignStampOptions(0, 72, 72, SignerName: "Ada"));
        System.Text.Encoding.ASCII.GetString(signed, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task Html_to_pdf_returns_valid_pdf()
    {
        var html = new PdfSharpHtmlConverter(NullLogger<PdfSharpHtmlConverter>.Instance);
        var pdf = await html.FromHtmlAsync("<h1>Hello</h1><p>Nam-Dev core</p><ul><li>One</li><li>Two</li></ul>");
        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
        pdf.Length.Should().BeGreaterThan(100);
    }
}
