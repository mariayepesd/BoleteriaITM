using Itm.Inventory.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Itm.Inventory.Api.Data;

public class FestivalInventoryDbContext : DbContext
{
    public FestivalInventoryDbContext(DbContextOptions<FestivalInventoryDbContext> options)
        : base(options) { }

    public DbSet<Evento> Eventos => Set<Evento>();
    public DbSet<BoleteriaItem> BoleteriaItems => Set<BoleteriaItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Evento>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            e.Property(x => x.Sede).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<BoleteriaItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Categoria).HasMaxLength(20).IsRequired();
            e.HasOne(x => x.Evento)
             .WithMany(ev => ev.Items)
             .HasForeignKey(x => x.EventoId);
        });

        // Datos semilla del festival
        modelBuilder.Entity<Evento>().HasData(
            new Evento { Id = 1, Nombre = "Festival de los Dos Mundos - Medellín", Descripcion = "El mayor festival de música en Colombia", Fecha = new DateTime(2026, 8, 15), Sede = "Medellin", EsActivo = true },
            new Evento { Id = 2, Nombre = "Festival de los Dos Mundos - Madrid",   Descripcion = "El mayor festival de música en España",   Fecha = new DateTime(2026, 8, 15), Sede = "Madrid",   EsActivo = true }
        );

        modelBuilder.Entity<BoleteriaItem>().HasData(
            // Medellín
            new BoleteriaItem { Id = 1, EventoId = 1, Categoria = "General", StockTotal = 50000, StockDisponible = 50000 },
            new BoleteriaItem { Id = 2, EventoId = 1, Categoria = "VIP",     StockTotal = 10000, StockDisponible = 10000 },
            new BoleteriaItem { Id = 3, EventoId = 1, Categoria = "Palco",   StockTotal =  1000, StockDisponible =  1000 },
            // Madrid
            new BoleteriaItem { Id = 4, EventoId = 2, Categoria = "General", StockTotal = 50000, StockDisponible = 50000 },
            new BoleteriaItem { Id = 5, EventoId = 2, Categoria = "VIP",     StockTotal = 10000, StockDisponible = 10000 },
            new BoleteriaItem { Id = 6, EventoId = 2, Categoria = "Palco",   StockTotal =  1000, StockDisponible =  1000 }
        );
    }
}
