using Itm.Order.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Itm.Order.Api.Data;

public class FestivalOrdersDbContext : DbContext
{
    public FestivalOrdersDbContext(DbContextOptions<FestivalOrdersDbContext> options)
        : base(options) { }

    public DbSet<Orden> Ordenes => Set<Orden>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Orden>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UsuarioId).HasMaxLength(100).IsRequired();
            e.Property(x => x.MontoTotal).HasColumnType("decimal(18,2)");
            e.Property(x => x.Estado).HasConversion<string>(); // Guarda "Confirmada" en lugar de 3
            e.Property(x => x.CorrelationId).HasMaxLength(50);
        });
    }
}
