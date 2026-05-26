using Itm.Price.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Itm.Price.Api.Data;

public class PriceDbContext : DbContext
{
    public PriceDbContext(DbContextOptions<PriceDbContext> options) : base(options) { }

    public DbSet<EventoPrecio> EventoPrecios => Set<EventoPrecio>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventoPrecio>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.PrecioBase).HasColumnType("decimal(18,2)");
            e.Property(x => x.Categoria).HasMaxLength(20).IsRequired();
            e.Property(x => x.Moneda).HasMaxLength(5);
            e.HasIndex(x => new { x.BoleteriaItemId }).IsUnique();
        });

        // Datos semilla: precios base por categoría para cada sede
        // BoleteriaItemId 1-3 = Medellín | 4-6 = Madrid
        modelBuilder.Entity<EventoPrecio>().HasData(
            // Medellín — precios en COP
            new EventoPrecio { Id = 1, BoleteriaItemId = 1, NombreEvento = "Festival de los Dos Mundos", Sede = "Medellin", Categoria = "General", PrecioBase = 150_000m, Moneda = "COP", StockTotal = 50000, StockVendido = 0 },
            new EventoPrecio { Id = 2, BoleteriaItemId = 2, NombreEvento = "Festival de los Dos Mundos", Sede = "Medellin", Categoria = "VIP",     PrecioBase = 450_000m, Moneda = "COP", StockTotal = 10000, StockVendido = 0 },
            new EventoPrecio { Id = 3, BoleteriaItemId = 3, NombreEvento = "Festival de los Dos Mundos", Sede = "Medellin", Categoria = "Palco",   PrecioBase = 1_200_000m, Moneda = "COP", StockTotal = 1000, StockVendido = 0 },
            // Madrid — precios en EUR
            new EventoPrecio { Id = 4, BoleteriaItemId = 4, NombreEvento = "Festival de los Dos Mundos", Sede = "Madrid", Categoria = "General", PrecioBase = 45m,  Moneda = "EUR", StockTotal = 50000, StockVendido = 0 },
            new EventoPrecio { Id = 5, BoleteriaItemId = 5, NombreEvento = "Festival de los Dos Mundos", Sede = "Madrid", Categoria = "VIP",     PrecioBase = 120m, Moneda = "EUR", StockTotal = 10000, StockVendido = 0 },
            new EventoPrecio { Id = 6, BoleteriaItemId = 6, NombreEvento = "Festival de los Dos Mundos", Sede = "Madrid", Categoria = "Palco",   PrecioBase = 350m, Moneda = "EUR", StockTotal = 1000,  StockVendido = 0 }
        );
    }
}
