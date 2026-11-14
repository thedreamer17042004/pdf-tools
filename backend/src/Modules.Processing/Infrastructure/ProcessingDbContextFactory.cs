using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Modules.Processing.Infrastructure;

public sealed class ProcessingDbContextFactory : IDesignTimeDbContextFactory<ProcessingDbContext>
{
    public ProcessingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ProcessingDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=acman;Username=acman;Password=acman_dev_password")
            .Options;
        return new ProcessingDbContext(options);
    }
}
