namespace decorativeplant_be.Application.Services;

public interface IOrderRealtimeNotifier
{
    Task NotifyOrderStatusUpdatedAsync(Guid orderId, string status, string source);
}
