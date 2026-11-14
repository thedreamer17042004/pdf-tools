using Microsoft.EntityFrameworkCore;
using Modules.Documents.Domain;

namespace Modules.Documents.Infrastructure;

public sealed class DocumentsDbContext : DbContext
{
    public DocumentsDbContext(DbContextOptions<DocumentsDbContext> options) : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentAnnotation> Annotations => Set<DocumentAnnotation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Document>();
        entity.ToTable("documents");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.FileName).HasMaxLength(512).IsRequired();
        entity.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
        entity.Property(x => x.StorageKey).HasMaxLength(1024).IsRequired();
        entity.Property(x => x.SizeBytes).IsRequired();
        entity.Property(x => x.PageCount).IsRequired();
        entity.Property(x => x.CreatedAtUtc).IsRequired();
        entity.Property(x => x.OwnerUserId);
        // PostgreSQL xmin optimistic concurrency (shadow property).
        entity.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
        entity.HasIndex(x => x.CreatedAtUtc);
        entity.HasIndex(x => x.DeletedAtUtc);
        entity.HasIndex(x => x.OwnerUserId);
        entity.HasQueryFilter(x => x.DeletedAtUtc == null);

        var ann = modelBuilder.Entity<DocumentAnnotation>();
        ann.ToTable("document_annotations");
        ann.HasKey(x => x.Id);
        ann.Property(x => x.Id).ValueGeneratedNever();
        ann.Property(x => x.Type).HasMaxLength(64).IsRequired();
        ann.Property(x => x.Color).HasMaxLength(32);
        ann.Property(x => x.Contents).HasMaxLength(4000);
        ann.Property(x => x.Author).HasMaxLength(256);
        ann.HasIndex(x => x.DocumentId);
        ann.HasIndex(x => new { x.DocumentId, x.PageIndex });
        ann.HasQueryFilter(x => x.DeletedAtUtc == null);
        ann.HasOne(x => x.Document)
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
