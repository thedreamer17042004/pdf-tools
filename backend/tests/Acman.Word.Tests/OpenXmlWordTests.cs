using Acman.Word.Abstractions.Contracts;
using Acman.Word.Engine.OpenXml;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acman.Word.Tests;

public class OpenXmlWordTests
{
    private readonly IWordDocumentService _words =
        new OpenXmlWordDocumentService(NullLogger<OpenXmlWordDocumentService>.Instance);

    [Fact]
    public async Task Create_merge_extract_roundtrip()
    {
        var created = await _words.CreateFromTextAsync(
            "Hello {{FullName}}\nDate: «Date»",
            new WordCreateOptions("Template", "Nam-Dev"));

        await using var listIn = new MemoryStream(created);
        var fields = await _words.ListMergeFieldsAsync(listIn);
        fields.Should().Contain(new[] { "FullName", "Date" });

        await using var mergeIn = new MemoryStream(created);
        var merged = await _words.MergeFieldsAsync(mergeIn, new Dictionary<string, string>
        {
            ["FullName"] = "Ada Lovelace",
            ["Date"] = "2026-11-14"
        });

        await using var textIn = new MemoryStream(merged);
        var text = await _words.ExtractTextAsync(textIn);
        text.Should().Contain("Ada Lovelace");
        text.Should().Contain("2026-11-14");
        text.Should().NotContain("{{FullName}}");
    }

    [Fact]
    public async Task Replace_and_info()
    {
        var created = await _words.CreateFromTextAsync("Nam-Dev draft");
        await using var replaceIn = new MemoryStream(created);
        var replaced = await _words.ReplaceTextAsync(replaceIn, "draft", "final");

        await using var infoIn = new MemoryStream(replaced);
        var info = await _words.GetInfoAsync(infoIn);
        info.ParagraphCount.Should().BeGreaterThan(0);

        await using var textIn = new MemoryStream(replaced);
        (await _words.ExtractTextAsync(textIn)).Should().Contain("final");
    }

    [Fact]
    public async Task Word_to_pdf_via_openxml_pdfsharp()
    {
        var converter = new OpenXmlWordToPdfConverter(NullLogger<OpenXmlWordToPdfConverter>.Instance);
        var created = await _words.CreateFromTextAsync("Word to PDF\nSecond line");
        await using var stream = new MemoryStream(created);
        var pdf = await converter.ConvertAsync(stream, new WordToPdfOptions(Title: "Test"));
        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
        pdf.Length.Should().BeGreaterThan(100);
    }
}
