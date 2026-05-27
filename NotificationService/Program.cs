using MassTransit;
using NotificationService.Consumers;
using NotificationService.Hubs;

var builder = WebApplication.CreateBuilder(args);

// --- SignalR para notificaciones en tiempo real ---
builder.Services.AddSignalR();

// --- CORS: permite conexiones WebSocket desde la app MAUI ---
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
});

// --- MassTransit + RabbitMQ ---
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderConfirmedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "amqp://guest:guest@localhost:5672");
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseCors();

app.MapHub<TicketHub>("/hubs/tickets");
app.MapHealthChecks("/health");

app.Run();
