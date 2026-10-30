using KnowledgeAssistant.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeAssistant.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Document>(document =>
        {
            document.ToTable("documents");
            document.HasKey(d => d.Id);
            document.Property(d => d.Title).HasMaxLength(DocumentLimits.TitleMaxLength).IsRequired();
            document.Property(d => d.Content).IsRequired();
            document.Property(d => d.CreatedAt).IsRequired();
        });
    }
}
