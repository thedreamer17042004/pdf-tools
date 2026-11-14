using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Processing.Application;
using Modules.Processing.Infrastructure;
using Modules.Processing.Messaging;

namespace Modules.Processing.DependencyInjection;

public static class ProcessingModuleExtensions
{
    public static IServiceCollection AddProcessingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

        services.AddDbContext<ProcessingDbContext>(options =>
        {
            var cs = configuration.GetConnectionString("Database")
                     ?? "Host=localhost;Port=5433;Database=acman;Username=acman;Password=acman_dev_password";
            options.UseNpgsql(cs);
        });

        services.AddHttpContextAccessor();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ToolCommandHandlers).Assembly));
        services.AddValidatorsFromAssemblyContaining<EnqueueMergeValidator>();
        services.AddScoped<IJobExecutor, JobExecutor>();

        var rabbitEnabled = configuration.GetSection(RabbitMqOptions.SectionName).GetValue("Enabled", true);
        if (rabbitEnabled)
        {
            services.AddSingleton<RabbitMqJobQueue>();
            services.AddSingleton<IJobQueue>(sp => sp.GetRequiredService<RabbitMqJobQueue>());
        }
        else
        {
            services.AddSingleton<IJobQueue, InlineJobQueue>();
        }

        return services;
    }
}
