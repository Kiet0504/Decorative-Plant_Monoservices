using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace decorativeplant_be.API.Hubs;

[Authorize]
public class OrderHub : Hub
{
    // Client gọi để nhận update của một order cụ thể
    public async Task SubscribeToOrder(string orderId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"order-{orderId}");
    }

    public async Task UnsubscribeFromOrder(string orderId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"order-{orderId}");
    }

    public async Task SubscribeToReturn(string returnId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"return-{returnId}");
    }

    public async Task UnsubscribeFromReturn(string returnId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"return-{returnId}");
    }
}
