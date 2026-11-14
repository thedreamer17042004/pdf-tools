using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Acman.Pdf.Abstractions.Contracts;
using Acman.Pdf.Abstractions.Exceptions;
using Microsoft.Extensions.Logging;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace Acman.Pdf.Engine.PdfSharp;

/// <summary>
/// Lightweight HTML → PDF (paragraphs / headings / lists). Not a Chromium render.
/// </summary>
public sealed partial class PdfSharpHtmlConverter : IPdfHtmlConverter
{
    private readonly ILogger<PdfSharpHtmlConverter> _logger;

    public PdfSharpHtmlConverter(ILogger<PdfSharpHtmlConverter> logger)
    {
        _logger = logger;
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        }
    }

    public Task<byte[]> FromHtmlAsync(
        string html,
        HtmlToPdfOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new HtmlToPdfOptions();
        try
        {
            var blocks = ParseBlocks(html);
            using var doc = new PdfDocument();
            if (!string.IsNullOrWhiteSpace(options.Title))
            {
                doc.Info.Title = options.Title;
            }

            doc.Info.Creator = "Nam-Dev HTML→PDF";
            var page = doc.AddPage();
            page.Size = global::PdfSharp.PageSize.A4;
            var margin = options.MarginPoints;
            var y = page.Height.Point - margin;
            var maxWidth = page.Width.Point - margin * 2;
            var gfx = XGraphics.FromPdfPage(page);
            var bodyFont = new XFont("Arial", options.FontSize, XFontStyleEx.Regular);
            var boldFont = new XFont("Arial", options.FontSize, XFontStyleEx.Bold);
            var brush = XBrushes.Black;

            foreach (var block in blocks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var font = block.HeadingLevel > 0
                    ? new XFont("Arial", options.FontSize + (4 - Math.Min(block.HeadingLevel, 3)) * 2, XFontStyleEx.Bold)
                    : (block.Bold ? boldFont : bodyFont);
                var lines = Wrap(gfx, block.Text, font, maxWidth);
                foreach (var line in lines)
                {
                    var lineHeight = font.Size * 1.35;
                    if (y - lineHeight < margin)
                    {
                        gfx.Dispose();
                        page = doc.AddPage();
                        page.Size = global::PdfSharp.PageSize.A4;
                        gfx = XGraphics.FromPdfPage(page);
                        y = page.Height.Point - margin;
                    }

                    y -= lineHeight;
                    gfx.DrawString(line, font, brush, new XPoint(margin, y));
                }

                y -= options.FontSize * 0.6;
            }

            gfx.Dispose();
            var pageCount = doc.PageCount;
            using var ms = new MemoryStream();
            doc.Save(ms, false);
            _logger.LogInformation("Converted HTML to PDF ({Pages} page(s), {Blocks} block(s))", pageCount, blocks.Count);
            return Task.FromResult(ms.ToArray());
        }
        catch (Exception ex)
        {
            throw new PdfOpenException("Failed to convert HTML to PDF.", ex);
        }
    }

    private static List<HtmlBlock> ParseBlocks(string html)
    {
        var text = html ?? string.Empty;
        text = ScriptStyleRegex().Replace(text, " ");
        text = text.Replace("\r\n", "\n");
        text = BlockBreakRegex().Replace(text, "\n");
        text = HeadingRegex().Replace(text, m => $"\n#H{m.Groups[1].Value}# {m.Groups[2].Value}\n");
        text = ListItemRegex().Replace(text, m => $"\n• {m.Groups[1].Value}\n");
        text = TagRegex().Replace(text, " ");
        text = WebUtility.HtmlDecode(text);
        text = WhitespaceRegex().Replace(text, " ").Trim();

        var blocks = new List<HtmlBlock>();
        foreach (var raw in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var heading = HeadingMarkerRegex().Match(raw);
            if (heading.Success)
            {
                blocks.Add(new HtmlBlock(heading.Groups[2].Value.Trim(), int.Parse(heading.Groups[1].Value), false));
            }
            else
            {
                blocks.Add(new HtmlBlock(raw, 0, false));
            }
        }

        if (blocks.Count == 0)
        {
            blocks.Add(new HtmlBlock("(empty HTML)", 0, false));
        }

        return blocks;
    }

    private static List<string> Wrap(XGraphics gfx, string text, XFont font, double maxWidth)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = new StringBuilder();
        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (gfx.MeasureString(candidate, font).Width <= maxWidth)
            {
                current.Clear();
                current.Append(candidate);
            }
            else
            {
                if (current.Length > 0) lines.Add(current.ToString());
                current.Clear();
                current.Append(word);
            }
        }

        if (current.Length > 0) lines.Add(current.ToString());
        if (lines.Count == 0) lines.Add(string.Empty);
        return lines;
    }

    private sealed record HtmlBlock(string Text, int HeadingLevel, bool Bold);

    [GeneratedRegex(@"<(script|style)[\s\S]*?</\1>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptStyleRegex();

    [GeneratedRegex(@"</?(p|div|br|tr|table|section|article)[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockBreakRegex();

    [GeneratedRegex(@"<h([1-6])[^>]*>([\s\S]*?)</h\1>", RegexOptions.IgnoreCase)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"<li[^>]*>([\s\S]*?)</li>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItemRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"[ \t\f\v]+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^#H([1-6])#\s*(.*)$")]
    private static partial Regex HeadingMarkerRegex();
}
