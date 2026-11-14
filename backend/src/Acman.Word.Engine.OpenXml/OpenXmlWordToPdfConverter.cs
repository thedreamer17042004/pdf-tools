using System.Text;
using Acman.Word.Abstractions.Contracts;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using A = DocumentFormat.OpenXml.Wordprocessing;

namespace Acman.Word.Engine.OpenXml;

/// <summary>
/// Self-built DOCX → PDF renderer: Open XML content model → PdfSharp layout.
/// Not a full Word layout clone; good for text, headings, and simple tables.
/// </summary>
public sealed class OpenXmlWordToPdfConverter : IWordToPdfConverter
{
    private readonly ILogger<OpenXmlWordToPdfConverter> _logger;

    public OpenXmlWordToPdfConverter(ILogger<OpenXmlWordToPdfConverter> logger)
    {
        _logger = logger;
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        }
    }

    public Task<byte[]> ConvertAsync(
        Stream source,
        WordToPdfOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new WordToPdfOptions();
        if (source.CanSeek) source.Position = 0;

        using var input = new MemoryStream();
        source.CopyTo(input);
        input.Position = 0;

        using var word = WordprocessingDocument.Open(input, false);
        using var pdf = new PdfDocument();
        if (!string.IsNullOrWhiteSpace(options.Title))
        {
            pdf.Info.Title = options.Title;
        }
        else if (!string.IsNullOrWhiteSpace(word.PackageProperties.Title))
        {
            pdf.Info.Title = word.PackageProperties.Title;
        }

        pdf.Info.Creator = "Nam-Dev Word→PDF (Open XML + PdfSharp)";

        var margin = options.MarginPoints;
        var layout = new PageLayout(pdf, margin);
        var contentWidth = layout.Page.Width.Point - margin * 2;
        var body = word.MainDocumentPart?.Document?.Body;

        if (body is not null)
        {
            foreach (var element in body.Elements())
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (element)
                {
                    case A.Paragraph paragraph:
                        layout.EnsureSpace(EstimateParagraphHeight(paragraph, options, layout.Gfx, contentWidth));
                        layout.Y = DrawParagraph(layout.Gfx, paragraph, margin, layout.Y, contentWidth, options);
                        layout.Y -= options.BodyFontSize * 0.55;
                        break;

                    case A.Table table:
                        layout.EnsureSpace(EstimateTableHeight(table, options));
                        layout.Y = DrawTable(layout.Gfx, table, margin, layout.Y, contentWidth, options);
                        layout.Y -= options.BodyFontSize * 0.8;
                        break;
                }
            }
        }

        layout.DisposeGfx();
        var pageCount = pdf.PageCount;
        using var output = new MemoryStream();
        pdf.Save(output, false);
        _logger.LogInformation(
            "Converted DOCX→PDF via Open XML/PdfSharp engine ({Pages} page(s), {Bytes} bytes)",
            pageCount,
            output.Length);
        return Task.FromResult(output.ToArray());
    }

    private sealed class PageLayout
    {
        private readonly PdfDocument _pdf;
        private readonly double _margin;

        public PageLayout(PdfDocument pdf, double margin)
        {
            _pdf = pdf;
            _margin = margin;
            Page = pdf.AddPage();
            Page.Size = global::PdfSharp.PageSize.A4;
            Gfx = XGraphics.FromPdfPage(Page);
            Y = Page.Height.Point - margin;
        }

        public PdfPage Page { get; private set; }
        public XGraphics Gfx { get; private set; }
        public double Y { get; set; }

        public void EnsureSpace(double needed)
        {
            if (Y - needed >= _margin) return;
            Gfx.Dispose();
            Page = _pdf.AddPage();
            Page.Size = global::PdfSharp.PageSize.A4;
            Gfx = XGraphics.FromPdfPage(Page);
            Y = Page.Height.Point - _margin;
        }

        public void DisposeGfx() => Gfx.Dispose();
    }

    private static double DrawParagraph(
        XGraphics gfx,
        A.Paragraph paragraph,
        double x,
        double yTop,
        double maxWidth,
        WordToPdfOptions options)
    {
        var runs = CollectRuns(paragraph, options);
        if (runs.Count == 0)
        {
            return yTop - options.BodyFontSize * 0.8;
        }

        var y = yTop;
        var line = new StringBuilder();
        XFont? lineFont = null;
        XBrush lineBrush = XBrushes.Black;

        void FlushLine()
        {
            if (line.Length == 0 || lineFont is null) return;
            var height = lineFont.Size * 1.35;
            y -= height;
            gfx.DrawString(line.ToString(), lineFont, lineBrush, new XPoint(x, y));
            line.Clear();
        }

        foreach (var run in runs)
        {
            lineFont ??= run.Font;
            lineBrush = run.Brush;
            var words = SplitKeepSpaces(run.Text);
            foreach (var word in words)
            {
                var candidate = line + word;
                if (gfx.MeasureString(candidate, run.Font).Width <= maxWidth || line.Length == 0)
                {
                    // Font may change mid-line; keep first font for simplicity on same line.
                    line.Append(word);
                    lineFont = run.Font;
                }
                else
                {
                    FlushLine();
                    lineFont = run.Font;
                    line.Append(word.TrimStart());
                }
            }
        }

        FlushLine();
        return y;
    }

    private static double DrawTable(
        XGraphics gfx,
        A.Table table,
        double x,
        double yTop,
        double maxWidth,
        WordToPdfOptions options)
    {
        var rows = table.Elements<A.TableRow>().ToList();
        if (rows.Count == 0) return yTop;

        var colCount = rows.Max(r => r.Elements<A.TableCell>().Count());
        colCount = Math.Max(colCount, 1);
        var colWidth = maxWidth / colCount;
        var font = new XFont("Arial", options.BodyFontSize, XFontStyleEx.Regular);
        var pen = new XPen(XColors.DimGray, 0.6);
        var y = yTop;

        foreach (var row in rows)
        {
            var cells = row.Elements<A.TableCell>().ToList();
            var cellTexts = new string[colCount];
            var rowHeight = options.BodyFontSize * 1.8;
            for (var c = 0; c < colCount; c++)
            {
                var text = c < cells.Count
                    ? string.Concat(cells[c].Descendants<A.Text>().Select(t => t.Text))
                    : string.Empty;
                cellTexts[c] = text;
                var lines = Math.Max(1, (int)Math.Ceiling(gfx.MeasureString(text, font).Width / Math.Max(8, colWidth - 8)));
                rowHeight = Math.Max(rowHeight, lines * font.Size * 1.35 + 8);
            }

            y -= rowHeight;
            for (var c = 0; c < colCount; c++)
            {
                var cellX = x + c * colWidth;
                gfx.DrawRectangle(pen, cellX, y, colWidth, rowHeight);
                gfx.DrawString(
                    TruncateToWidth(gfx, cellTexts[c], font, colWidth - 10),
                    font,
                    XBrushes.Black,
                    new XPoint(cellX + 5, y + rowHeight - font.Size - 4));
            }
        }

        return y;
    }

    private static List<StyledRun> CollectRuns(A.Paragraph paragraph, WordToPdfOptions options)
    {
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? string.Empty;
        var headingLevel = 0;
        if (styleId.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(styleId.AsSpan("Heading".Length), out var level))
        {
            headingLevel = Math.Clamp(level, 1, 6);
        }

        var baseSize = headingLevel > 0
            ? options.BodyFontSize + (5 - Math.Min(headingLevel, 4)) * 2.5
            : options.BodyFontSize;
        var baseBold = headingLevel > 0;

        var list = new List<StyledRun>();
        foreach (var run in paragraph.Elements<A.Run>())
        {
            var text = string.Concat(run.Elements<A.Text>().Select(t => t.Text));
            if (text.Length == 0) continue;

            var rPr = run.RunProperties;
            var bold = baseBold || rPr?.Bold is not null || rPr?.BoldComplexScript is not null;
            var italic = rPr?.Italic is not null || rPr?.ItalicComplexScript is not null;
            var style = (bold, italic) switch
            {
                (true, true) => XFontStyleEx.BoldItalic,
                (true, false) => XFontStyleEx.Bold,
                (false, true) => XFontStyleEx.Italic,
                _ => XFontStyleEx.Regular
            };

            var size = baseSize;
            if (rPr?.FontSize?.Val?.Value is { } sz && int.TryParse(sz, out var halfPoints) && halfPoints > 0)
            {
                size = halfPoints / 2.0;
            }

            list.Add(new StyledRun(text, new XFont("Arial", size, style), XBrushes.Black));
        }

        // Fallback: concatenated text if runs empty but paragraph has text descendants
        if (list.Count == 0)
        {
            var all = string.Concat(paragraph.Descendants<A.Text>().Select(t => t.Text));
            if (!string.IsNullOrEmpty(all))
            {
                list.Add(new StyledRun(
                    all,
                    new XFont("Arial", baseSize, baseBold ? XFontStyleEx.Bold : XFontStyleEx.Regular),
                    XBrushes.Black));
            }
        }

        return list;
    }

    private static double EstimateParagraphHeight(
        A.Paragraph paragraph,
        WordToPdfOptions options,
        XGraphics gfx,
        double maxWidth)
    {
        var runs = CollectRuns(paragraph, options);
        if (runs.Count == 0) return options.BodyFontSize;
        var font = runs[0].Font;
        var text = string.Concat(runs.Select(r => r.Text));
        var width = Math.Max(1, gfx.MeasureString(text, font).Width);
        var lines = Math.Max(1, (int)Math.Ceiling(width / maxWidth));
        return lines * font.Size * 1.35 + options.BodyFontSize * 0.55;
    }

    private static double EstimateTableHeight(A.Table table, WordToPdfOptions options)
    {
        var rows = table.Elements<A.TableRow>().Count();
        return Math.Max(1, rows) * (options.BodyFontSize * 1.8 + 4) + options.BodyFontSize;
    }

    private static IEnumerable<string> SplitKeepSpaces(string text)
    {
        var sb = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (sb.Length > 0)
                {
                    yield return sb.ToString();
                    sb.Clear();
                }

                yield return ch.ToString();
            }
            else
            {
                sb.Append(ch);
            }
        }

        if (sb.Length > 0) yield return sb.ToString();
    }

    private static string TruncateToWidth(XGraphics gfx, string text, XFont font, double maxWidth)
    {
        if (string.IsNullOrEmpty(text) || gfx.MeasureString(text, font).Width <= maxWidth)
        {
            return text;
        }

        const string ellipsis = "…";
        for (var len = text.Length - 1; len > 0; len--)
        {
            var candidate = text[..len] + ellipsis;
            if (gfx.MeasureString(candidate, font).Width <= maxWidth)
            {
                return candidate;
            }
        }

        return ellipsis;
    }

    private sealed record StyledRun(string Text, XFont Font, XBrush Brush);
}
