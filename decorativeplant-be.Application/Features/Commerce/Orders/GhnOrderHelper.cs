using System.Text.Json;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Commerce.Orders;

/// <summary>
/// Creates a single GHN shipping order for an <see cref="OrderHeader"/>.
/// Design B: 1 OrderHeader = 1 branch = 1 shipment. All <see cref="OrderItem"/>s
/// on the header are guaranteed to share the same BranchId by CreateOrderHandler.
/// </summary>
public static class GhnOrderHelper
{
    public static async Task<bool> TryCreateGhnOrderAsync(OrderHeader order, IShippingService shippingService, ILogger logger)
    {
        logger.LogInformation("GHN: Starting shipment creation for Order {OrderCode} (ID: {OrderId})", order.OrderCode, order.Id);

        if (order.DeliveryAddress == null)
        {
            logger.LogWarning("GHN: Skipping - DeliveryAddress is null for Order {OrderCode}", order.OrderCode);
            MarkHandoffFailure(order, "DeliveryAddress missing");
            return false;
        }
        if (order.OrderItems == null || order.OrderItems.Count == 0)
        {
            logger.LogWarning("GHN: Skipping - No OrderItems for Order {OrderCode}", order.OrderCode);
            MarkHandoffFailure(order, "No order items");
            return false;
        }

        if (order.Notes != null && order.Notes.RootElement.TryGetProperty("shipments", out _))
        {
            logger.LogInformation("GHN: Skipping - Shipments already exist for Order {OrderCode}", order.OrderCode);
            return true;
        }

        try
        {
            var da = order.DeliveryAddress.RootElement;

            string GetProp(params string[] keys) {
                foreach (var k in keys) if (da.TryGetProperty(k, out var p)) return p.GetString() ?? "";
                return "";
            }
            int GetIntProp(int def, params string[] keys) {
                foreach (var k in keys) if (da.TryGetProperty(k, out var p)) return p.ValueKind == JsonValueKind.Number ? p.GetInt32() : (int.TryParse(p.GetString(), out var v) ? v : def);
                return def;
            }

            var toName = GetProp("recipientName", "recipient_name");
            var toPhone = GetProp("phone");
            var toAddress = GetProp("addressLine1", "address_line_1");
            var toDistrict = GetIntProp(1454, "districtId", "district_id");
            var toWard = GetProp("wardCode", "ward_code");

            if (string.IsNullOrEmpty(toName) || string.IsNullOrEmpty(toPhone))
            {
                logger.LogWarning("GHN: Critical missing address info for Order {OrderCode}. Name={Name}, Phone={Phone}", order.OrderCode, toName, toPhone);
            }

            logger.LogInformation("GHN: Delivery address for Order {OrderCode}: Name={Name}, Phone={Phone}, Address={Address}, District={District}, Ward={Ward}",
                order.OrderCode, toName, toPhone, toAddress, toDistrict, toWard);

            var totalStr = order.Financials?.RootElement.TryGetProperty("total", out var t) == true ? (t.GetString() ?? "0") : "0";
            var orderTotal = (int)decimal.Parse(totalStr);

            // Resolve payment_method from TypeInfo. COD orders must tell GHN to collect
            // cash from the buyer (cod_amount) and bill shipping to buyer (payment_type_id=2).
            var isCod = order.TypeInfo != null
                && order.TypeInfo.RootElement.TryGetProperty("payment_method", out var pm)
                && string.Equals(pm.GetString(), "cod", StringComparison.OrdinalIgnoreCase);

            var ghnItems = order.OrderItems.Select(oi => {
                var name = "Decorative Plant";
                if (oi.Snapshots != null && oi.Snapshots.RootElement.TryGetProperty("title_snapshot", out var ts))
                    name = ts.GetString() ?? name;
                return new ShippingOrderItem { Name = name, Quantity = oi.Quantity, Weight = 1000 };
            }).ToList();

            var branchId = order.OrderItems.First().BranchId;
            var clientCode = order.OrderCode ?? order.Id.ToString();

            var res = await shippingService.CreateOrderAsync(new ShippingOrderRequest {
                ToName = toName, ToPhone = toPhone, ToAddress = toAddress, ToDistrictId = toDistrict, ToWardCode = toWard,
                FromDistrictId = shippingService.DefaultFromDistrictId, FromWardCode = shippingService.DefaultFromWardCode,
                Weight = ghnItems.Sum(i => i.Quantity) * 1000, InsuranceValue = orderTotal, ClientOrderCode = clientCode, Items = ghnItems,
                CodAmount = isCod ? orderTotal : 0, PaymentTypeId = isCod ? 2 : 1
            });

            if (!res.Success || string.IsNullOrEmpty(res.OrderCode))
            {
                logger.LogError("GHN Error for Order {OrderCode}: {Message}", order.OrderCode, res.Message);
                MarkHandoffFailure(order, res.Message ?? "Unknown GHN error");
                return false;
            }

            var shipment = new { branch_id = branchId?.ToString(), tracking_code = res.OrderCode, carrier = "GHN" };

            var notes = new Dictionary<string, object?>();
            if (order.Notes != null)
                foreach (var prop in order.Notes.RootElement.EnumerateObject())
                    if (prop.Name != "shipments" && prop.Name != "ghn_handoff_failed" && prop.Name != "ghn_handoff_error")
                        notes[prop.Name] = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.GetRawText();
            // Keep JSONB shape backward-compatible (array) — FE/staff dashboards already read shipments[].
            notes["shipments"] = new[] { shipment };
            order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes));

            // GHN order just created ≈ `ready_to_pick`. Auto-advance the order to `processing`
            // (Đang chờ lấy hàng) so the customer immediately sees the post-confirm phase
            // without waiting for the first GHN webhook.
            OrderStatusMachine.ApplyFromExternalSource(order, OrderStatusMachine.Processing,
                source: "GHN", reason: "GHN order created, awaiting pickup");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GHN Error for {OrderCode}", order.OrderCode);
            MarkHandoffFailure(order, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Stamps the order with a visible failure flag so the staff dashboard can surface
    /// "carrier handoff needs retry" instead of silently leaving the order stuck in confirmed.
    /// </summary>
    private static void MarkHandoffFailure(OrderHeader order, string reason)
    {
        var notes = new Dictionary<string, object?>();
        if (order.Notes != null)
            foreach (var prop in order.Notes.RootElement.EnumerateObject())
                notes[prop.Name] = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.GetRawText();
        notes["ghn_handoff_failed"] = true;
        notes["ghn_handoff_error"] = reason;
        notes["ghn_handoff_failed_at"] = DateTime.UtcNow.ToString("o");
        order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes));
    }
}
