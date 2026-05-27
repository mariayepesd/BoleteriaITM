using Microsoft.AspNetCore.SignalR;

namespace NotificationService.Hubs;

public class TicketHub : Hub
{
    public async Task JoinGroup(string customerId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, customerId);
    }
}