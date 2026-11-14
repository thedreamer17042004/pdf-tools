namespace Acman.Pdf.Abstractions.Contracts;

/// <summary>Aspose-like AcroForm API: list, fill, merge-fields, flatten, remove.</summary>
public interface IPdfFormService
{
    Task<IReadOnlyList<PdfFormFieldInfo>> ListFieldsAsync(
        Stream source,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string?>> ExportValuesAsync(
        Stream source,
        CancellationToken cancellationToken = default);

    Task<byte[]> FillFieldsAsync(
        Stream source,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mail-merge style: fill named fields from a data dictionary, optionally flatten.
    /// </summary>
    Task<byte[]> MergeFieldsAsync(
        Stream source,
        IReadOnlyDictionary<string, string> values,
        MergeFieldsOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<byte[]> FlattenAsync(
        Stream source,
        CancellationToken cancellationToken = default);

    Task<byte[]> RemoveFieldsAsync(
        Stream source,
        IReadOnlyList<string>? fieldNames = null,
        CancellationToken cancellationToken = default);

    /// <summary>Create interactive AcroForm fields (text / checkbox) — iLovePDF Forms-style.</summary>
    Task<byte[]> AddFieldsAsync(
        Stream source,
        IReadOnlyList<PdfFormFieldCreate> fields,
        CancellationToken cancellationToken = default);
}

public sealed record PdfFormFieldCreate(
    string Name,
    string FieldType,
    int PageIndex,
    double XPoints,
    double YPoints,
    double WidthPoints,
    double HeightPoints,
    string? Value = null);

public sealed record MergeFieldsOptions(bool Flatten = false);

public sealed record PdfFormFieldInfo(
    string Name,
    string FieldType,
    string? Value,
    bool ReadOnly);
