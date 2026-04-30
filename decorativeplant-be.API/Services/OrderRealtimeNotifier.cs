using decorativeplant_be.API.Hubs;
using decorativeplant_be.Application.Services;
using Microsoft.AspNetCore.SignalR;

namespace decorativeplant_be.API.Services;

public class OrderRealtimeNotifier : IOrderRealtimeNotifier
{
    private readonly IHubContext<OrderHub> _hubContext;

    public OrderRealtimeNotifier(IHubContext<OrderHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyOrderStatusUpdatedAsync(Guid orderId, string status, string source)
    {
        await _hubContext.Clients
            .Group($"order-{orderId}")
            .SendAsync("OrderStatusUpdated", new
            {
                orderId,
                status,
                source,
                updatedAt = DateTime.UtcNow
            });
    }
}
