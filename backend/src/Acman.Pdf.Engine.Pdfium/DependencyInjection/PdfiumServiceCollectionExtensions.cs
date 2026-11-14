using Acman.Pdf.Abstractions.Contracts;
using Acman.Pdf.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Acman.Pdf.Engine.Pdfium.DependencyInjection;

public static class PdfiumServiceCollectionExtensions
{
    public static IServiceCollection AddNamDevPdfiumEngine(this IServiceCollection services)
    {
        services.TryAddSingleton<IPdfDocumentFactory, PdfiumDocumentFactory>();
        services.TryAddSingleton<IPdfRenderer, PdfiumRenderer>();
        services.TryAddSingleton<IPdfTextExtractor, PdfiumTextExtractor>();
        services.TryAddSingleton<IPdfCompareService, PdfiumCompareService>();
        services.TryAddSingleton<IPdfMarkdownConverter, PdfiumMarkdownConverter>();
        services.TryAddScoped<PdfDocumentService>();
        return services;
    }
}
