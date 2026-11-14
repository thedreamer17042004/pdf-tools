using Acman.Pdf.Abstractions.Storage;
using Acman.Pdf.Infrastructure.Caching;
using Acman.Pdf.Infrastructure.Options;
using Acman.Pdf.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acman.Pdf.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddNamDevPdfInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<DocumentsOptions>(configuration.GetSection(DocumentsOptions.SectionName));
        services.Configure<RedisOptions>(opts =>
        {
            configuration.GetSection(RedisOptions.SectionName).Bind(opts);
            var cs = configuration.GetConnectionString("Redis");
            if (!string.IsNullOrWhiteSpace(cs))
            {
                opts.ConnectionString = cs;
            }
        });

        var provider = configuration.GetSection(StorageOptions.SectionName)["Provider"] ?? "FileSystem";
        if (string.Equals(provider, "S3", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IObjectStorage, S3ObjectStorage>();
        }
        else
        {
            services.AddSingleton<IObjectStorage, FileSystemObjectStorage>();
        }

        var redisEnabled = configuration.GetSection(RedisOptions.SectionName).GetValue("Enabled", true);
        if (redisEnabled)
        {
            services.AddSingleton<IRenderCache, RedisRenderCache>();
        }
        else
        {
            services.AddSingleton<IRenderCache, NullRenderCache>();
        }

        return services;
    }
}
