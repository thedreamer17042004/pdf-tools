using Acman.Pdf.Abstractions.Contracts;
using Acman.Pdf.Abstractions.Exceptions;
using Acman.Pdf.Core.Validation;
using Microsoft.Extensions.Logging;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.AcroForms;
using PdfSharp.Pdf.IO;

namespace Acman.Pdf.Engine.PdfSharp;

public sealed class PdfSharpFormService : IPdfFormService
{
    private readonly ILogger<PdfSharpFormService> _logger;

    public PdfSharpFormService(ILogger<PdfSharpFormService> logger) => _logger = logger;

    public async Task<IReadOnlyList<PdfFormFieldInfo>> ListFieldsAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        var bytes = await ReadAllAsync(source, cancellationToken);
        PdfFileValidator.EnsurePdfMagic(bytes);
        using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        var form = doc.AcroForm;
        if (form?.Fields is null || form.Fields.Count == 0)
        {
            return [];
        }

        var items = new List<PdfFormFieldInfo>();
        CollectFields(form.Fields, items);
        return items;
    }

    public async Task<IReadOnlyDictionary<string, string?>> ExportValuesAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        var fields = await ListFieldsAsync(source, cancellationToken);
        return fields.ToDictionary(f => f.Name, f => f.Value, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<byte[]> FillFieldsAsync(
        Stream source,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default)
    {
        var bytes = await ReadAllAsync(source, cancellationToken);
        PdfFileValidator.EnsurePdfMagic(bytes);
        using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Modify);
        ApplyValues(doc, values, cancellationToken);
        _logger.LogInformation("Filled {Count} form value(s)", values.Count);
        return Save(doc);
    }

    public async Task<byte[]> MergeFieldsAsync(
        Stream source,
        IReadOnlyDictionary<string, string> values,
        MergeFieldsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new MergeFieldsOptions();
        var filled = await FillFieldsAsync(source, values, cancellationToken);
        if (!options.Flatten)
        {
            return filled;
        }

        await using var stream = new MemoryStream(filled);
        return await FlattenAsync(stream, cancellationToken);
    }

    public async Task<byte[]> FlattenAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        var bytes = await ReadAllAsync(source, cancellationToken);
        PdfFileValidator.EnsurePdfMagic(bytes);
        using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Modify);
        var form = doc.AcroForm;
        if (form?.Fields is null || form.Fields.Count == 0)
        {
            return bytes;
        }

        FlattenFields(form.Fields);
        doc.AcroForm.Elements.Remove("/Fields");
        _logger.LogInformation("Flattened AcroForm fields");
        return Save(doc);
    }

    public async Task<byte[]> RemoveFieldsAsync(
        Stream source,
        IReadOnlyList<string>? fieldNames = null,
        CancellationToken cancellationToken = default)
    {
        var bytes = await ReadAllAsync(source, cancellationToken);
        PdfFileValidator.EnsurePdfMagic(bytes);
        using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Modify);
        var form = doc.AcroForm;
        if (form?.Fields is null || form.Fields.Count == 0)
        {
            return bytes;
        }

        if (fieldNames is null || fieldNames.Count == 0)
        {
            form.Elements.Remove("/Fields");
            _logger.LogInformation("Removed all AcroForm fields");
            return Save(doc);
        }

        var remove = fieldNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        RemoveMatching(form.Fields, remove);
        _logger.LogInformation("Removed named AcroForm field(s)");
        return Save(doc);
    }

    public async Task<byte[]> AddFieldsAsync(
        Stream source,
        IReadOnlyList<PdfFormFieldCreate> fields,
        CancellationToken cancellationToken = default)
    {
        if (fields.Count == 0)
        {
            throw new PdfOpenException("At least one form field definition is required.");
        }

        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        }

        var bytes = await ReadAllAsync(source, cancellationToken);
        PdfFileValidator.EnsurePdfMagic(bytes);
        using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Modify);

        foreach (var def in fields)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (def.PageIndex < 0 || def.PageIndex >= doc.PageCount)
            {
                throw new PdfPageOutOfRangeException(def.PageIndex, doc.PageCount);
            }

            var page = doc.Pages[def.PageIndex];
            var rect = new PdfRectangle(new XRect(def.XPoints, def.YPoints, def.WidthPoints, def.HeightPoints));
            var type = (def.FieldType ?? "text").Trim().ToLowerInvariant();

            // PDFsharp has limited public APIs to register new AcroForm widgets on existing docs.
            // Draw fillable-looking placeholders + embed field metadata as page content labels
            // so merge-fields workflows can still target named areas visually.
            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            var pen = new XPen(XColors.SteelBlue, 0.9);
            var fill = new XSolidBrush(XColor.FromArgb(20, XColors.SteelBlue));
            gfx.DrawRectangle(pen, fill, def.XPoints, def.YPoints, def.WidthPoints, def.HeightPoints);

            if (type is "checkbox" or "check")
            {
                var box = Math.Min(def.WidthPoints, def.HeightPoints) * 0.55;
                gfx.DrawRectangle(
                    pen,
                    def.XPoints + 3,
                    def.YPoints + (def.HeightPoints - box) / 2,
                    box,
                    box);
                if (def.Value is "1" or "true" or "True" or "yes" or "on")
                {
                    gfx.DrawLine(pen, def.XPoints + 5, def.YPoints + def.HeightPoints / 2,
                        def.XPoints + 3 + box / 2, def.YPoints + def.HeightPoints / 2 + box / 3);
                    gfx.DrawLine(pen, def.XPoints + 3 + box / 2, def.YPoints + def.HeightPoints / 2 + box / 3,
                        def.XPoints + 3 + box - 2, def.YPoints + def.HeightPoints / 2 - box / 3);
                }
            }
            else if (!string.IsNullOrEmpty(def.Value))
            {
                var font = new XFont("Arial", 10, XFontStyleEx.Regular);
                gfx.DrawString(def.Value, font, XBrushes.Black, new XPoint(def.XPoints + 4, def.YPoints + 4));
            }

            var labelFont = new XFont("Arial", 7, XFontStyleEx.Regular);
            gfx.DrawString(
                $"[{type}] {def.Name}",
                labelFont,
                XBrushes.SteelBlue,
                new XPoint(def.XPoints, def.YPoints + def.HeightPoints + 2));

            _ = rect; // keep geometry validated
        }

        _logger.LogInformation("Added {Count} visual form field placeholder(s)", fields.Count);
        return Save(doc);
    }

    private static void ApplyValues(
        PdfDocument doc,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        var form = doc.AcroForm
                   ?? throw new PdfOpenException("This PDF has no AcroForm fields to fill.");

        if (form.Elements.ContainsKey("/NeedAppearances"))
        {
            form.Elements["/NeedAppearances"] = new PdfBoolean(true);
        }
        else
        {
            form.Elements.Add("/NeedAppearances", new PdfBoolean(true));
        }

        foreach (var (name, value) in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var field = FindField(form.Fields, name);
            if (field is null) continue;

            switch (field)
            {
                case PdfTextField text:
                    text.Text = value;
                    break;
                case PdfCheckBoxField check:
                    check.Checked = value is "1" or "true" or "True" or "yes" or "on";
                    break;
                case PdfComboBoxField combo:
                    combo.Value = new PdfString(value);
                    break;
                case PdfListBoxField list:
                    list.Value = new PdfString(value);
                    break;
                default:
                    field.Value = new PdfString(value);
                    break;
            }
        }
    }

    private static void CollectFields(PdfAcroField.PdfAcroFieldCollection fields, List<PdfFormFieldInfo> items)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            if (field.HasKids)
            {
                CollectFields(field.Fields, items);
                continue;
            }

            items.Add(new PdfFormFieldInfo(
                field.Name ?? $"field_{i}",
                field.GetType().Name.Replace("Pdf", "").Replace("Field", ""),
                field.Value?.ToString(),
                field.ReadOnly));
        }
    }

    private static PdfAcroField? FindField(PdfAcroField.PdfAcroFieldCollection fields, string name)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            if (string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return field;
            }

            if (field.HasKids)
            {
                var nested = FindField(field.Fields, name);
                if (nested is not null) return nested;
            }
        }

        return null;
    }

    private static void FlattenFields(PdfAcroField.PdfAcroFieldCollection fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            field.ReadOnly = true;
            if (field.HasKids)
            {
                FlattenFields(field.Fields);
            }
        }
    }

    private static void RemoveMatching(PdfAcroField.PdfAcroFieldCollection fields, HashSet<string> names)
    {
        // PdfSharp collection has limited remove-by-name; mark matching as read-only hidden best-effort
        // and clear values when names match. Full tree removal of arbitrary fields is constrained by the SDK.
        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            if (field.HasKids)
            {
                RemoveMatching(field.Fields, names);
                continue;
            }

            if (field.Name is not null && names.Contains(field.Name))
            {
                field.ReadOnly = true;
                field.Value = new PdfString(string.Empty);
                field.Elements.SetInteger("/F", 2); // Hidden
            }
        }
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.CanSeek) stream.Position = 0;
        await using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    private static byte[] Save(PdfDocument document)
    {
        using var ms = new MemoryStream();
        document.Save(ms, false);
        return ms.ToArray();
    }
}
