using Acman.Pdf.Abstractions.Models;

namespace Acman.Pdf.Abstractions.Contracts;

public interface IXfdfService
{
    string Export(IEnumerable<PdfAnnotation> annotations, string? pdfFileName = null);
    IReadOnlyList<PdfAnnotation> Import(string xfdfXml);
}
