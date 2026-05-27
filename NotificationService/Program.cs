using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Festival.Contracts;
using NotificationService.Hubs;

namespace NotificationService.Consumers;

public class OrderConfirmedConsumer : IConsumer<OrderConfirmedEvent>
{
    // 1. Cambiamos el nombre aquí para evitar que choque con otra cosa
    private readonly IHubContext<TicketHub> _ticketHub;

    public OrderConfirmedConsumer(IHubContext<TicketHub> hubContext)
    {
        // 2. Asignamos el parámetro al nuevo nombre
        _ticketHub = hubContext;
    }

    public async Task Consume(ConsumeContext<OrderConfirmedEvent> context)
    {
        var orden = context.Message;

        await Task.Delay(2000);
        string urlBoleta = $"https://storage.itm.edu.co/tickets/{orden.OrderId}.pdf";

        // 3. Usamos la variable renombrada libre de ambigüedades
        await _ticketHub.Clients.Group(orden.CustomerId).SendAsync("ReceiveTicketReady", new
        {
            OrderId = orden.OrderId,
            TicketUrl = urlBoleta,
            Message = "¡Tu boleta para el Festival ya está lista!"
        });
    }
}