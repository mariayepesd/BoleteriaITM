using System.Text.Json;
using Itm.Price.Api.Data;
using Itm.Price.Api.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- Base de datos SQL Server ---
builder.Services.AddDbContext<PriceDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Caché distribuida con Redis ---
// Esta es la pieza clave de la Fase 2: el 90% de las consultas deben responderse desde aquí
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:ConnectionString"];
    options.InstanceName = "FestivalPrices:";
});

var app = builder.Build();

// --- Aplica migraciones al arrancar ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PriceDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ===========================================================================
// GET /api/prices/{boleteriaItemId}
// Flujo: Redis → (cache miss) → SQL Server → calcular precio dinámico → Redis
// ===========================================================================
app.MapGet("/api/prices/{boleteriaItemId}", async (int boleteriaItemId, PriceDbContext db, IDistributedCache cache) =>
{
    var cacheKey = $"price:{boleteriaItemId}";

    // --- PASO 1: Consultar Redis (caché) ---
    var cached = await cache.GetStringAsync(cacheKey);
    if (cached is not null)
    {
        // CACHE HIT: respuesta en < 1ms sin tocar la base de datos
        var cachedPrice = JsonSerializer.Deserialize<PriceDto>(cached)!;
        return Results.Ok(cachedPrice with { FromCache = true });
    }

    // --- PASO 2: CACHE MISS — consultar SQL Server ---
    var item = await db.EventoPrecios.FirstOrDefaultAsync(e => e.BoleteriaItemId == boleteriaItemId);

    if (item is null)
        return Results.NotFound(new { Error = $"No hay precios configurados para el item {boleteriaItemId}" });

    // --- PASO 3: Calcular precio dinámico según ocupación ---
    //
    //  Lógica de precios dinámicos (como aerolíneas y plataformas de streaming):
    //
    //  Ocupación > 80%  → ×1.5  (demanda muy alta, últimas entradas)
    //  Ocupación > 50%  → ×1.2  (demanda alta, más de la mitad vendido)
    //  Ocupación ≤ 50%  → ×1.0  (precio normal)
    //
    var porcentajeOcupacion = item.StockTotal > 0
        ? (double)item.StockVendido / item.StockTotal
        : 0.0;

    var (multiplicador, nivelDemanda) = porcentajeOcupacion switch
    {
        > 0.80 => (1.5m, "Muy Alta"),
        > 0.50 => (1.2m, "Alta"),
        _      => (1.0m, "Normal")
    };

    var precioFinal = Math.Round(item.PrecioBase * multiplicador, 2);

    var priceDto = new PriceDto(
        BoleteriaItemId : item.BoleteriaItemId,
        NombreEvento    : item.NombreEvento,
        Sede            : item.Sede,
        Categoria       : item.Categoria,
        PrecioBase      : item.PrecioBase,
        PrecioFinal     : precioFinal,
        Multiplicador   : multiplicador,
        Moneda          : item.Moneda,
        NivelDemanda    : nivelDemanda,
        FromCache       : false
    );

    // --- PASO 4: Guardar en Redis por 5 minutos ---
    var cacheOptions = new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };
    await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(priceDto), cacheOptions);

    return Results.Ok(priceDto);
});

// ===========================================================================
// POST /api/prices/invalidate/{boleteriaItemId}
// Invalida manualmente el caché cuando el stock cambia drásticamente.
// En producción, esto lo dispararía un evento de MassTransit.
// ===========================================================================
app.MapPost("/api/prices/invalidate/{boleteriaItemId}", async (int boleteriaItemId, IDistributedCache cache) =>
{
    await cache.RemoveAsync($"price:{boleteriaItemId}");
    return Results.Ok(new { Message = $"Caché invalidado para item {boleteriaItemId}. Próxima consulta irá a SQL." });
});

// ===========================================================================
// PATCH /api/prices/{boleteriaItemId}/vendidos
// Actualiza las boletas vendidas (en producción esto llega por evento MassTransit)
// ===========================================================================
app.MapPatch("/api/prices/{boleteriaItemId}/vendidos", async (int boleteriaItemId, int cantidad, PriceDbContext db, IDistributedCache cache) =>
{
    var item = await db.EventoPrecios.FirstOrDefaultAsync(e => e.BoleteriaItemId == boleteriaItemId);
    if (item is null) return Results.NotFound();

    item.StockVendido = Math.Min(item.StockVendido + cantidad, item.StockTotal);
    await db.SaveChangesAsync();

    // Invalida el caché para que el próximo GET calcule el precio actualizado
    await cache.RemoveAsync($"price:{boleteriaItemId}");

    Console.WriteLine($"[PRECIO] Item {boleteriaItemId}: {item.StockVendido}/{item.StockTotal} vendidos ({(double)item.StockVendido / item.StockTotal:P0} ocupación)");
    return Results.Ok(new { item.BoleteriaItemId, item.StockVendido, item.StockTotal });
});

app.Run();
