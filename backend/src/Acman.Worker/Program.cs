using Acman.Pdf.Engine.Pdfium.DependencyInjection;
using Acman.Pdf.Engine.PdfSharp.DependencyInjection;
using Acman.Pdf.Infrastructure.DependencyInjection;
using Acman.Worker;
using Modules.Documents.DependencyInjection;
using Modules.Processing.DependencyInjection;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((_, cfg) => cfg
        .MinimumLevel.Information()
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddNamDevPdfiumEngine();
    builder.Services.AddNamDevPdfSharpEngine();
    builder.Services.AddNamDevPdfInfrastructure(builder.Configuration);
    builder.Services.AddDocumentsModule(builder.Configuration);
    builder.Services.AddProcessingModule(builder.Configuration);
    builder.Services.AddHostedService<ProcessingJobConsumer>();

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Nam-Dev Worker terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
