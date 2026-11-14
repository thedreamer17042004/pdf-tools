using Acman.Word.Abstractions.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Acman.Word.Engine.OpenXml.DependencyInjection;

public static class WordOpenXmlServiceCollectionExtensions
{
    public static IServiceCollection AddNamDevWordOpenXmlEngine(this IServiceCollection services)
    {
        services.TryAddSingleton<IWordDocumentService, OpenXmlWordDocumentService>();
        services.TryAddSingleton<IWordToPdfConverter, OpenXmlWordToPdfConverter>();
        return services;
    }
}
