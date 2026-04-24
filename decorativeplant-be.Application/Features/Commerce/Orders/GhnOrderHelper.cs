using System.Text.Json;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common;
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

        // Only skip when we already have a non-empty shipments array (not merely a key or empty array).
        if (order.Notes != null
            && order.Notes.RootElement.TryGetProperty("shipments", out var existingShipments)
            && existingShipments.ValueKind == JsonValueKind.Array
            && existingShipments.GetArrayLength() > 0)
        {
            logger.LogInformation("GHN: Skipping - Shipments already exist for Order {OrderCode}", order.OrderCode);
            return true;
        }

        try
        {
            // Staff offline ship-from-branch: DeliveryAddress JSON is GHN-only (name, phone, address, district, ward).
            var da = order.DeliveryAddress.RootElement;

            /// <summary>GHN ward / phone may arrive as JSON string or number depending on channel serialization.</summary>
            static string ReadStringish(JsonElement p)
            {
                return p.ValueKind switch
                {
                    JsonValueKind.String => p.GetString() ?? "",
                    JsonValueKind.Number => p.GetRawText().Trim(),
                    _ => "",
                };
            }

            string GetProp(params string[] keys)
            {
                foreach (var k in keys)
                {
                    if (!da.TryGetProperty(k, out var p)) continue;
                    var s = ReadStringish(p);
                    if (!string.IsNullOrEmpty(s)) return s;
                }

                return "";
            }

            int GetIntProp(int def, params string[] keys)
            {
                foreach (var k in keys)
                {
                    if (!da.TryGetProperty(k, out var p)) continue;
                    if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i)) return i;
                    if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var v)) return v;
                }

                return def;
            }

            var toName = GetProp("recipientName", "recipient_name");
            var toPhone = GetProp("phone");
            var toAddress = GetProp("addressLine1", "address_line_1");
            var toDistrict = GetIntProp(1454, "districtId", "district_id");
            var toWard = GetProp("wardCode", "ward_code");

            if (string.IsNullOrEmpty(toName) || string.IsNullOrEmpty(toPhone))
            {
                var missing = string.IsNullOrEmpty(toName) && string.IsNullOrEmpty(toPhone) ? "name and phone" :
                              string.IsNullOrEmpty(toName) ? "name" : "phone";
                logger.LogError("GHN: Cannot create shipment for Order {OrderCode} — delivery address is missing {Missing}.", order.OrderCode, missing);
                MarkHandoffFailure(order, $"Delivery address missing: {missing}");
                return false;
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
                return new ShippingOrderItem { Name = name, Quantity = oi.Quantity, Weight = 500 };
            }).ToList();

            var branchId = order.OrderItems.First().BranchId;
            var clientCode = $"DECPLANT-{order.OrderCode ?? order.Id.ToString()}";

            var res = await shippingService.CreateOrderAsync(new ShippingOrderRequest {
                ToName = toName, ToPhone = toPhone, ToAddress = toAddress, ToDistrictId = toDistrict, ToWardCode = toWard,
                FromDistrictId = shippingService.DefaultFromDistrictId, FromWardCode = shippingService.DefaultFromWardCode,
                Weight = Math.Max(ghnItems.Sum(i => i.Quantity) * 500, 500),
                InsuranceValue = Math.Min(orderTotal, 5_000_000), ClientOrderCode = clientCode, Items = ghnItems,
                ServiceTypeId = shippingService.DefaultServiceTypeId,
                CodAmount = isCod ? orderTotal : 0, PaymentTypeId = isCod ? 2 : 1
            });

            if (!res.Success || string.IsNullOrEmpty(res.OrderCode))
            {
                logger.LogError("GHN Error for Order {OrderCode}: {Message}", order.OrderCode, res.Message);
                MarkHandoffFailure(order, res.Message ?? "Unknown GHN error");
                return false;
            }

            var shipment = new { branch_id = branchId?.ToString(), tracking_code = res.OrderCode, carrier = "GHN" };

            var notes = OfflineDeliveryDeliveredMailHelper.CloneNotesToDictionary(order);
            notes.Remove("shipments");
            notes.Remove("ghn_handoff_failed");
            notes.Remove("ghn_handoff_error");
            notes.Remove("ghn_handoff_failed_at");
            // Keep JSONB shape backward-compatible (array) — FE/staff dashboards already read shipments[].
            notes["shipments"] = new[] { shipment };
            order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes));

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
        var notes = OfflineDeliveryDeliveredMailHelper.CloneNotesToDictionary(order);
        notes["ghn_handoff_failed"] = true;
        notes["ghn_handoff_error"] = reason;
        notes["ghn_handoff_failed_at"] = DateTime.UtcNow.ToString("o");
        order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes));
    }
}
