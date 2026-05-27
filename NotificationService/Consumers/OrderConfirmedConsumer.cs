using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Festival.Contracts;
using NotificationService.Hubs;

namespace NotificationService.Consumers;

public class OrderConfirmedConsumer : IConsumer<OrderConfirmedEvent>
{
    private readonly IHubContext<TicketHub> _hubContext;

    public OrderConfirmedConsumer(IHubContext<TicketHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task Consume(ConsumeContext<OrderConfirmedEvent> context)
    {
        var orden = context.Message;

        // Simulación de generación de boleta
        await Task.Delay(2000);
        string urlBoleta = $"https://storage.itm.edu.co/tickets/{orden.OrderId}.pdf";

        // Enviar a SignalR al grupo del cliente
        await _hubContext.Clients.Group(orden.CustomerId).SendAsync("ReceiveTicketReady", new
        {
            OrderId = orden.OrderId,
            TicketUrl = urlBoleta,
            Message = "¡Tu boleta para el Festival ya está lista!"
        });
    }
}