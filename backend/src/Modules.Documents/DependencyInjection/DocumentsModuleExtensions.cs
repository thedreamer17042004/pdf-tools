using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Documents.Application;
using Modules.Documents.Infrastructure;

namespace Modules.Documents.DependencyInjection;

public static class DocumentsModuleExtensions
{
    public static IServiceCollection AddDocumentsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<DocumentsDbContext>(options =>
        {
            var cs = configuration.GetConnectionString("Database")
                     ?? "Host=localhost;Port=5433;Database=acman;Username=acman;Password=acman_dev_password";
            options.UseNpgsql(cs);
        });

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(UploadDocumentHandler).Assembly));
        services.AddValidatorsFromAssemblyContaining<UploadDocumentValidator>();

        return services;
    }
}
