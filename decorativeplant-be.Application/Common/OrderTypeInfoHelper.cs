using System.Collections.Frozen;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Common;

/// <summary>
/// Reads <see cref="OrderHeader.TypeInfo"/> JSON for channel / fulfillment rules.
/// </summary>
public static class OrderTypeInfoHelper
{
    /// <summary>Staff / branch order_type values (see CreateOrderRequest / counter flows).</summary>
    private static readonly FrozenSet<string> NoAutoFulfillmentAssignOrderTypes =
        new[] { "offline", "offline_pickup", "bopis_immediate" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True for store- or branch-created orders (not web checkout <c>online</c>).
    /// </summary>
    public static bool IsOfflineChannelOrder(OrderHeader? order)
    {
        if (order?.TypeInfo == null) return false;
        var root = order.TypeInfo.RootElement;
        var ot = root.TryGetProperty("order_type", out var el) ? el.GetString() : null;
        return ot != null && NoAutoFulfillmentAssignOrderTypes.Contains(ot);
    }

    /// <summary>In-store / branch pickup (web BOPIS-style or counter pickup).</summary>
    public static bool IsPickupAtBranchOrder(OrderHeader? order)
    {
        if (order?.TypeInfo == null) return false;
        var fm = order.TypeInfo.RootElement.TryGetProperty("fulfillment_method", out var el) ? el.GetString() : null;
        return string.Equals(fm?.Trim(), "pickup", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Ship-to-customer delivery; legacy rows without the field are treated as delivery.</summary>
    public static bool IsDeliveryFulfillment(OrderHeader? order)
    {
        if (order?.TypeInfo == null) return true;
        var fm = order.TypeInfo.RootElement.TryGetProperty("fulfillment_method", out var el) ? el.GetString() : null;
        if (string.IsNullOrWhiteSpace(fm)) return true;
        return string.Equals(fm.Trim(), "delivery", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Counter/ship offline + home delivery participates in the same queue as online delivery.
    /// Excluded: in-store pickup, and offline workflows that are not ship-to-customer (e.g. bopis_transfer).
    /// </summary>
    public static bool SkipsFulfillmentWorkloadPipeline(OrderHeader? order)
    {
        if (order == null) return true;
        if (IsPickupAtBranchOrder(order)) return true;
        if (IsOfflineChannelOrder(order) && !IsDeliveryFulfillment(order)) return true;
        return false;
    }

    /// <summary>Store staff offline order with ship-to-customer delivery (counter / GHN path).</summary>
    public static bool IsOfflineCounterDeliveryShipOrder(OrderHeader? order)
        => IsOfflineChannelOrder(order) && IsDeliveryFulfillment(order);
}
