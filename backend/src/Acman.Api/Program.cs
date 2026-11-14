using Acman.Api.Endpoints;
using Acman.Api.Middleware;
using Acman.Pdf.Engine.Pdfium.DependencyInjection;
using Acman.Pdf.Engine.PdfSharp.DependencyInjection;
using Acman.Pdf.Infrastructure.DependencyInjection;
using Acman.Word.Engine.OpenXml.DependencyInjection;
using Acman.Pdf.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Modules.Documents.DependencyInjection;
using Modules.Documents.Infrastructure;
using Modules.Identity.DependencyInjection;
using Modules.Identity.Infrastructure;
using Modules.Processing.DependencyInjection;
using Modules.Processing.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddNamDevPdfiumEngine();
    builder.Services.AddNamDevPdfSharpEngine();
    builder.Services.AddNamDevWordOpenXmlEngine();
    builder.Services.AddNamDevPdfInfrastructure(builder.Configuration);
    builder.Services.AddDocumentsModule(builder.Configuration);
    builder.Services.AddIdentityModule(builder.Configuration);
    builder.Services.AddProcessingModule(builder.Configuration);

    builder.Services.AddOpenApi();
    builder.Services.AddProblemDetails();
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("web", policy =>
            policy.WithOrigins(
                    builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                    ?? ["http://localhost:3000"])
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
    });

    var dbCs = builder.Configuration.GetConnectionString("Database")
               ?? "Host=localhost;Port=5433;Database=acman;Username=acman;Password=acman_dev_password";
    var redisCs = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

    builder.Services.AddHealthChecks()
        .AddNpgSql(dbCs, name: "postgres")
        .AddRedis(redisCs, name: "redis");

    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize =
            builder.Configuration.GetSection(DocumentsOptions.SectionName)
                .GetValue("MaxUploadBytes", 50L * 1024 * 1024);
    });

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseCors("web");
    app.UseAuthentication();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.MapHealthChecks("/health");
    app.MapAuthEndpoints();
    app.MapDocumentEndpoints();
    app.MapAnnotationEndpoints();
    app.MapToolEndpoints();
    app.MapPdfCoreEndpoints();
    app.MapWordCoreEndpoints();

    using (var scope = app.Services.CreateScope())
    {
        var documents = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        await documents.Database.MigrateAsync();

        var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await identity.Database.MigrateAsync();

        var processing = scope.ServiceProvider.GetRequiredService<ProcessingDbContext>();
        await processing.Database.MigrateAsync();
    }

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Nam-Dev API terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
