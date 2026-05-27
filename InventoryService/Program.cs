using System.Text;
using Itm.Inventory.Api.Core.Interfaces;
using Itm.Inventory.Api.Data;
using Itm.Inventory.Api.Dtos;
using Itm.Inventory.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- Base de datos SQL Server ---
builder.Services.AddDbContext<FestivalInventoryDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Autenticación JWT ---
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(secretKey)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Administrador"));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddHealthChecks();

var app = builder.Build();

// --- Aplica migraciones y siembra datos al arrancar ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FestivalInventoryDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// GET /api/inventory/{id} — Obtiene un item de boletería por ID
app.MapGet("/api/inventory/{id}", async (int id, FestivalInventoryDbContext db, HttpContext ctx, ILogger<Program> logger) =>
{
    var correlationId = ctx.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? "SIN-ID";
    using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
    {
        var item = await db.BoleteriaItems
            .Include(b => b.Evento)
            .FirstOrDefaultAsync(b => b.Id == id);

        return item is not null
            ? Results.Ok(new
            {
                item.Id,
                Evento = item.Evento.Nombre,
                Sede = item.Evento.Sede,
                item.Categoria,
                item.StockDisponible,
                item.StockTotal
            })
            : Results.NotFound(new { Error = $"Item {id} no encontrado" });
    }
}).RequireAuthorization();

// POST /api/inventory/reduce — Reserva stock (Paso 1 de la SAGA)
app.MapPost("/api/inventory/reduce", async (ReduceStockDto request, FestivalInventoryDbContext db, ICurrentUserService currentUserService) =>
{
    var email = currentUserService.ObtenerEmailUsuario();
    Console.WriteLine($"[AUDITORÍA] {email} reserva {request.Quantity} boletas del item {request.ProductId}.");

    var item = await db.BoleteriaItems.FindAsync(request.ProductId);

    if (item is null)
        return Results.NotFound(new { Error = "Item de boletería no encontrado" });

    if (item.StockDisponible < request.Quantity)
        return Results.BadRequest(new { Error = "Stock insuficiente", StockActual = item.StockDisponible });

    item.StockDisponible -= request.Quantity;
    await db.SaveChangesAsync();

    return Results.Ok(new { Message = "Stock reservado correctamente", NuevoStock = item.StockDisponible });
}); // interno: llamado por OrderService (SAGA), no requiere JWT de usuario

// POST /api/inventory/release — Devuelve stock (Compensación SAGA)
app.MapPost("/api/inventory/release", async (ReduceStockDto request, FestivalInventoryDbContext db) =>
{
    var item = await db.BoleteriaItems.FindAsync(request.ProductId);
    if (item is null) return Results.NotFound(new { Error = "Item no encontrado" });

    item.StockDisponible += request.Quantity;
    await db.SaveChangesAsync();

    Console.WriteLine($"[COMPENSACIÓN SAGA] {request.Quantity} boletas devueltas al item {request.ProductId}. Stock: {item.StockDisponible}");
    return Results.Ok(new { Message = "Boletas devueltas al inventario", StockActual = item.StockDisponible });
});

// Servicio gRPC para consultas internas de stock (usado por otros microservicios)
app.MapGrpcService<GrpcInventoryService>();
app.MapHealthChecks("/health");

app.Run();
