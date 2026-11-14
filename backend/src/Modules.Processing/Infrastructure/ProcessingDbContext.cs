using Microsoft.EntityFrameworkCore;
using Modules.Processing.Domain;

namespace Modules.Processing.Infrastructure;

public sealed class ProcessingDbContext : DbContext
{
    public ProcessingDbContext(DbContextOptions<ProcessingDbContext> options) : base(options)
    {
    }

    public DbSet<ProcessingJob> Jobs => Set<ProcessingJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProcessingJob>();
        entity.ToTable("processing_jobs");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.PayloadJson).IsRequired();
        entity.Property(x => x.ResultStorageKey).HasMaxLength(1024);
        entity.Property(x => x.ResultFileName).HasMaxLength(512);
        entity.Property(x => x.ResultContentType).HasMaxLength(128);
        entity.Property(x => x.Error).HasMaxLength(4000);
        entity.HasIndex(x => x.Status);
        entity.HasIndex(x => x.CreatedAtUtc);
        entity.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
