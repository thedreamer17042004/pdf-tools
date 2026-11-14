using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Acman.Pdf.Abstractions.Contracts;
using Acman.Pdf.Abstractions.Models;

namespace Acman.Pdf.Core.Annotations;

/// <summary>
/// Subset XFDF serializer for Nam-Dev annotations (original implementation; not a Syncfusion port).
/// Supported: Highlight, Underline, StrikeOut, Squiggly, FreeText, Square, Circle, Ink, Text.
/// </summary>
public sealed class XfdfService : IXfdfService
{
    private static readonly XNamespace XfdfNs = "http://ns.adobe.com/xfdf/";

    public string Export(IEnumerable<PdfAnnotation> annotations, string? pdfFileName = null)
    {
        var xfdf = new XElement(XfdfNs + "xfdf",
            new XAttribute(XNamespace.Xmlns + "xfdf", XfdfNs.NamespaceName),
            new XElement(XfdfNs + "f", new XAttribute("href", pdfFileName ?? "document.pdf")),
            new XElement(XfdfNs + "annots",
                annotations.Select(ToXfdfAnnot).Where(x => x is not null)!));

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), xfdf);
        using var sw = new StringWriter();
        doc.Save(sw, SaveOptions.DisableFormatting);
        return sw.ToString();
    }

    public IReadOnlyList<PdfAnnotation> Import(string xfdfXml)
    {
        if (string.IsNullOrWhiteSpace(xfdfXml))
        {
            return [];
        }

        var doc = XDocument.Parse(xfdfXml);
        var annots = doc.Root?
            .Element(XfdfNs + "annots")?
            .Elements()
            .Select(FromXfdfAnnot)
            .Where(a => a is not null)
            .Cast<PdfAnnotation>()
            .ToList() ?? [];

        return annots;
    }

    private static XElement? ToXfdfAnnot(PdfAnnotation a)
    {
        var name = a.Type switch
        {
            PdfAnnotationType.Highlight => "highlight",
            PdfAnnotationType.Underline => "underline",
            PdfAnnotationType.Strikeout => "strikeout",
            PdfAnnotationType.Squiggly => "squiggly",
            PdfAnnotationType.FreeText => "freetext",
            PdfAnnotationType.Rectangle => "square",
            PdfAnnotationType.Circle => "circle",
            PdfAnnotationType.Ink => "ink",
            PdfAnnotationType.StickyNote => "text",
            _ => null
        };

        if (name is null)
        {
            return null;
        }

        var el = new XElement(XfdfNs + name,
            new XAttribute("page", a.PageIndex.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("rect", FormatRect(a.Rect)),
            new XAttribute("color", a.Color ?? "#FFFF00"),
            new XAttribute("opacity", a.Opacity.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("name", a.Id.ToString("N")),
            new XAttribute("date", a.UpdatedAtUtc.UtcDateTime.ToString("yyyyMMddHHmmsszzz", CultureInfo.InvariantCulture)));

        if (!string.IsNullOrWhiteSpace(a.Author))
        {
            el.SetAttributeValue("title", a.Author);
        }

        if (!string.IsNullOrWhiteSpace(a.Contents))
        {
            el.Add(new XElement(XfdfNs + "contents", a.Contents));
        }

        if (a.Quads is { Count: > 0 })
        {
            el.Add(new XElement(XfdfNs + "coords", string.Join(",", a.Quads.SelectMany(QuadToCoords))));
        }

        if (a.InkPaths is { Count: > 0 })
        {
            var inkList = new XElement(XfdfNs + "inklist");
            foreach (var path in a.InkPaths)
            {
                inkList.Add(new XElement(XfdfNs + "gesture",
                    string.Join(";", path.Select(p =>
                        $"{p.X.ToString(CultureInfo.InvariantCulture)},{p.Y.ToString(CultureInfo.InvariantCulture)}"))));
            }

            el.Add(inkList);
        }

        return el;
    }

    private static PdfAnnotation? FromXfdfAnnot(XElement el)
    {
        var local = el.Name.LocalName.ToLowerInvariant();
        var type = local switch
        {
            "highlight" => PdfAnnotationType.Highlight,
            "underline" => PdfAnnotationType.Underline,
            "strikeout" => PdfAnnotationType.Strikeout,
            "squiggly" => PdfAnnotationType.Squiggly,
            "freetext" => PdfAnnotationType.FreeText,
            "square" => PdfAnnotationType.Rectangle,
            "circle" => PdfAnnotationType.Circle,
            "ink" => PdfAnnotationType.Ink,
            "text" => PdfAnnotationType.StickyNote,
            _ => (PdfAnnotationType?)null
        };

        if (type is null)
        {
            return null;
        }

        var idAttr = (string?)el.Attribute("name");
        var id = Guid.TryParseExact(idAttr ?? string.Empty, "N", out var parsed) || Guid.TryParse(idAttr, out parsed)
            ? parsed
            : Guid.NewGuid();

        var page = int.TryParse((string?)el.Attribute("page"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var p)
            ? p
            : 0;

        var annot = new PdfAnnotation
        {
            Id = id,
            Type = type.Value,
            PageIndex = page,
            Rect = ParseRect((string?)el.Attribute("rect")),
            Color = (string?)el.Attribute("color") ?? "#FFFF00",
            Opacity = double.TryParse((string?)el.Attribute("opacity"), NumberStyles.Float, CultureInfo.InvariantCulture, out var op) ? op : 1,
            Author = (string?)el.Attribute("title"),
            Contents = el.Element(XfdfNs + "contents")?.Value
        };

        var coords = el.Element(XfdfNs + "coords")?.Value;
        if (!string.IsNullOrWhiteSpace(coords))
        {
            annot.Quads = ParseQuads(coords);
        }

        var gestures = el.Element(XfdfNs + "inklist")?.Elements(XfdfNs + "gesture").ToList();
        if (gestures is { Count: > 0 })
        {
            annot.InkPaths = gestures.Select(g => ParseGesture(g.Value)).ToList();
        }

        return annot;
    }

    private static string FormatRect(PdfRect r)
    {
        var x2 = r.X + r.Width;
        var y2 = r.Y + r.Height;
        return string.Join(",",
            r.X.ToString(CultureInfo.InvariantCulture),
            r.Y.ToString(CultureInfo.InvariantCulture),
            x2.ToString(CultureInfo.InvariantCulture),
            y2.ToString(CultureInfo.InvariantCulture));
    }

    private static PdfRect ParseRect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new PdfRect(0, 0, 0, 0);
        }

        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 4)
        {
            return new PdfRect(0, 0, 0, 0);
        }

        var x1 = double.Parse(parts[0], CultureInfo.InvariantCulture);
        var y1 = double.Parse(parts[1], CultureInfo.InvariantCulture);
        var x2 = double.Parse(parts[2], CultureInfo.InvariantCulture);
        var y2 = double.Parse(parts[3], CultureInfo.InvariantCulture);
        return new PdfRect(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));
    }

    private static IEnumerable<string> QuadToCoords(PdfRect q)
    {
        // XFDF coords: x1,y1,x2,y2,x3,y3,x4,y4 (four corners)
        yield return q.X.ToString(CultureInfo.InvariantCulture);
        yield return (q.Y + q.Height).ToString(CultureInfo.InvariantCulture);
        yield return (q.X + q.Width).ToString(CultureInfo.InvariantCulture);
        yield return (q.Y + q.Height).ToString(CultureInfo.InvariantCulture);
        yield return (q.X + q.Width).ToString(CultureInfo.InvariantCulture);
        yield return q.Y.ToString(CultureInfo.InvariantCulture);
        yield return q.X.ToString(CultureInfo.InvariantCulture);
        yield return q.Y.ToString(CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<PdfRect> ParseQuads(string coords)
    {
        var nums = coords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => double.Parse(s, CultureInfo.InvariantCulture))
            .ToArray();

        var quads = new List<PdfRect>();
        for (var i = 0; i + 7 < nums.Length; i += 8)
        {
            var xs = new[] { nums[i], nums[i + 2], nums[i + 4], nums[i + 6] };
            var ys = new[] { nums[i + 1], nums[i + 3], nums[i + 5], nums[i + 7] };
            var minX = xs.Min();
            var maxX = xs.Max();
            var minY = ys.Min();
            var maxY = ys.Max();
            quads.Add(new PdfRect(minX, minY, maxX - minX, maxY - minY));
        }

        return quads;
    }

    private static IReadOnlyList<PdfPoint> ParseGesture(string value)
    {
        return value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(pair =>
            {
                var xy = pair.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return new PdfPoint(
                    double.Parse(xy[0], CultureInfo.InvariantCulture),
                    double.Parse(xy[1], CultureInfo.InvariantCulture));
            })
            .ToList();
    }
}
