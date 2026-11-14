using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Modules.Documents.Infrastructure;

public sealed class DocumentsDbContextFactory : IDesignTimeDbContextFactory<DocumentsDbContext>
{
    public DocumentsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=acman;Username=acman;Password=acman_dev_password")
            .Options;
        return new DocumentsDbContext(options);
    }
}
