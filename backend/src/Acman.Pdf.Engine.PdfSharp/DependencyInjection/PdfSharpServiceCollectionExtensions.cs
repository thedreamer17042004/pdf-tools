using Acman.Pdf.Abstractions.Contracts;
using Acman.Pdf.Core.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Acman.Pdf.Engine.PdfSharp.DependencyInjection;

public static class PdfSharpServiceCollectionExtensions
{
    public static IServiceCollection AddNamDevPdfSharpEngine(this IServiceCollection services)
    {
        services.TryAddSingleton<IPdfManipulationService, PdfSharpManipulationService>();
        services.TryAddSingleton<IPdfFormService, PdfSharpFormService>();
        services.TryAddSingleton<IPdfDocumentInfoService, PdfSharpDocumentInfoService>();
        services.TryAddSingleton<IPdfBookmarkService, PdfSharpBookmarkService>();
        services.TryAddSingleton<IPdfHtmlConverter, PdfSharpHtmlConverter>();
        services.TryAddSingleton<IXfdfService, XfdfService>();
        return services;
    }
}
