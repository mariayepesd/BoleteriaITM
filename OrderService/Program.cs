using System.Net.Http.Json;
using Festival.Contracts;
using Itm.Inventory.Api.Protos;
using Itm.Order.Api.Data;
using Itm.Order.Api.Data.Entities;
using Itm.Order.Api.Handlers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;

// Requerido para gRPC sobre HTTP sin TLS en Docker
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- Base de datos SQL Server ---
builder.Services.AddDbContext<FestivalOrdersDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Cliente gRPC hacia InventoryService ---
builder.Services.AddGrpcClient<InventoryService.InventoryServiceClient>(o =>
{
    o.Address = new Uri(builder.Configuration["ServiceUrls:InventoryGrpc"] ?? "http://localhost:5273");
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CorrelationIdDelegatingHandler>();
builder.Services.AddHealthChecks();

// --- Clientes HTTP con resiliencia y propagación de Correlation ID ---
builder.Services
    .AddHttpClient("InventoryClient", client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["ServiceUrls:Inventory"] ?? "http://localhost:5273");
        client.Timeout = TimeSpan.FromSeconds(5);
    })
    .AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
    .AddStandardResilienceHandler();

builder.Services
    .AddHttpClient("PriceClient", client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["ServiceUrls:Price"] ?? "http://localhost:5285");
        client.Timeout = TimeSpan.FromSeconds(5);
    })
    .AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
    .AddStandardResilienceHandler();

// --- Productor MassTransit + RabbitMQ ---
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "amqp://guest:guest@localhost:5672");
    });
});

var app = builder.Build();

// --- Aplica migraciones al arrancar ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FestivalOrdersDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");

// POST /api/orders — Crea una orden con patrón SAGA orquestado
app.MapPost("/api/orders", async (CreateOrderDto dto, IHttpClientFactory factory, FestivalOrdersDbContext db, HttpContext ctx, IPublishEndpoint publishEndpoint) =>
{
    var correlationId = ctx.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString();

    // PASO 1: Registrar la orden como Pendiente en la base de datos
    var orden = new Orden
    {
        UsuarioId      = "usuario-demo", // En producción: extraer del JWT
        BoleteriaItemId = dto.ProductId,
        Cantidad       = dto.Quantity,
        MontoTotal     = 0,
        Estado         = OrdenEstado.Pendiente,
        FechaCreacion  = DateTime.UtcNow,
        CorrelationId  = correlationId
    };
    db.Ordenes.Add(orden);
    await db.SaveChangesAsync();

    Console.WriteLine($"[SAGA] Orden #{orden.Id} creada. Estado: Pendiente. CorrelationId: {correlationId}");

    // PASO 2: Reservar stock en InventoryService
    var invClient = factory.CreateClient("InventoryClient");
    var reduceResponse = await invClient.PostAsJsonAsync("/api/inventory/reduce",
        new { ProductId = dto.ProductId, Quantity = dto.Quantity });

    if (!reduceResponse.IsSuccessStatusCode)
    {
        orden.Estado = OrdenEstado.Cancelada;
        await db.SaveChangesAsync();
        Console.WriteLine($"[SAGA] Orden #{orden.Id} cancelada: sin stock.");
        return Results.BadRequest(new { Error = "Sin stock disponible. Orden cancelada.", OrdenId = orden.Id });
    }

    orden.Estado = OrdenEstado.StockReservado;
    await db.SaveChangesAsync();
    Console.WriteLine($"[SAGA] Orden #{orden.Id} — stock reservado.");

    // PASO 3: Consultar precio actual (Price.Api con Redis)
    decimal precioUnitario = 150_000m; // Fallback si Price.Api no está disponible
    try
    {
        var priceClient = factory.CreateClient("PriceClient");
        var priceResponse = await priceClient.GetFromJsonAsync<PriceResponse>($"/api/prices/{dto.ProductId}");
        if (priceResponse is not null) precioUnitario = priceResponse.Amount;
    }
    catch
    {
        Console.WriteLine("[SAGA] Price.Api no disponible. Usando precio de respaldo.");
    }

    // PASO 4: Procesar pago (simulación con 80% de éxito)
    try
    {
        var paymentSuccess = new Random().Next(0, 10) > 1;
        if (!paymentSuccess) throw new InvalidOperationException("Fondos insuficientes en la tarjeta.");

        orden.MontoTotal = precioUnitario * dto.Quantity;
        orden.Estado = OrdenEstado.Confirmada;
        await db.SaveChangesAsync();

        Console.WriteLine($"[SAGA] Orden #{orden.Id} confirmada. Total: ${orden.MontoTotal:N0}");

        await publishEndpoint.Publish<OrderConfirmedEvent>(new OrderConfirmedEvent
        {
            OrderId = Guid.NewGuid(), // O usa el GUID de tu correlación / ID de orden si aplica
            CustomerId = orden.UsuarioId, // "usuario-demo"
            EventName = "Festival de los Dos Mundos",
            TicketQuantity = orden.Cantidad,
            TotalAmount = orden.MontoTotal
        });

        return Results.Ok(new
        {
            OrdenId = orden.Id,
            Message = "¡Boletas compradas exitosamente!",
            Estado  = orden.Estado.ToString(),
            Total   = orden.MontoTotal,
            CorrelationId = correlationId
        });
    }
    catch (Exception ex)
    {
        // COMPENSACIÓN: devolver stock al inventario
        Console.WriteLine($"[SAGA] Pago fallido: {ex.Message}. Iniciando compensación...");

        var release = await invClient.PostAsJsonAsync("/api/inventory/release",
            new { ProductId = dto.ProductId, Quantity = dto.Quantity });

        orden.Estado = release.IsSuccessStatusCode ? OrdenEstado.Compensada : OrdenEstado.Cancelada;
        await db.SaveChangesAsync();

        Console.WriteLine($"[SAGA] Orden #{orden.Id} — estado final: {orden.Estado}");

        return orden.Estado == OrdenEstado.Compensada
            ? Results.Problem($"Pago fallido. Las {dto.Quantity} boletas fueron devueltas al sistema.")
            : Results.Problem("Error crítico: pago y compensación fallaron. Contacte soporte.");
    }
});

// GET /api/orders/{id} — Consulta el estado de una orden
app.MapGet("/api/orders/{id}", async (int id, FestivalOrdersDbContext db) =>
{
    var orden = await db.Ordenes.FindAsync(id);
    return orden is not null
        ? Results.Ok(new { orden.Id, orden.Estado, orden.MontoTotal, orden.FechaCreacion, orden.CorrelationId })
        : Results.NotFound(new { Error = $"Orden {id} no encontrada" });
});

// POST /api/orders/grpc — Verifica stock vía gRPC antes de crear la orden
app.MapPost("/api/orders/grpc", async (int productId, InventoryService.InventoryServiceClient grpcClient) =>
{
    var reply = await grpcClient.CheckStockAsync(new StockRequest { ProductId = productId });

    if (!reply.IsAvailable)
        return Results.BadRequest($"Stock insuficiente. Solo quedan {reply.Stock} unidades.");

    return Results.Ok(new { Message = "Stock disponible verificado vía gRPC", Stock = reply.Stock });
});

app.Run();

// DTOs
public record CreateOrderDto(int ProductId, int Quantity);
public record PriceResponse(int ProductId, decimal Amount, string Currency, bool FromCache);
