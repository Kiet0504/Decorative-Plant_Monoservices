using System.Text.Json;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Inventory;

public static class InventoryMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public static StockTransferDto ToStockTransferDto(StockTransfer transfer)
    {
        return new StockTransferDto
        {
            Id = transfer.Id,
            TransferCode = transfer.TransferCode,
            BatchId = transfer.BatchId ?? Guid.Empty,
            FromBranchId = transfer.FromBranchId ?? Guid.Empty,
            ToBranchId = transfer.ToBranchId ?? Guid.Empty,
            FromLocationId = transfer.FromLocationId ?? Guid.Empty,
            ToLocationId = transfer.ToLocationId ?? Guid.Empty,
            Quantity = transfer.Quantity,
            Status = transfer.Status ?? "requested",
            CreatedAt = transfer.CreatedAt ?? DateTime.UtcNow,
            ShippedAt = GetLogisticsDate(transfer.LogisticsInfo, "shipped_at"),
            ReceivedAt = GetLogisticsDate(transfer.LogisticsInfo, "received_at")
        };
    }

    public static JsonDocument BuildLogisticsInfo(
        Guid? requestedBy = null,
        string? notes = null,
        DateTime? shippedAt = null,
        Guid? shippedBy = null,
        string? trackingNumber = null,
        string? shippingProvider = null, 
        DateTime? receivedAt = null,
        Guid? receivedBy = null,
        string? receivingNotes = null,
        JsonDocument? existingInfo = null)
    {
        var dict = new Dictionary<string, object?>();
        
        if (existingInfo != null)
        {
            try 
            {
                var existing = JsonSerializer.Deserialize<Dictionary<string, object?>>(existingInfo.RootElement.GetRawText(), JsonOptions);
                if (existing != null) dict = existing;
            }
            catch { /* Ignore invalid existing JSON */ }
        }

        if (requestedBy.HasValue) dict["requested_by"] = requestedBy;
        if (!string.IsNullOrEmpty(notes)) dict["notes"] = notes;
        
        if (shippedAt.HasValue) dict["shipped_at"] = shippedAt;
        if (shippedBy.HasValue) dict["shipped_by"] = shippedBy;
        if (!string.IsNullOrEmpty(trackingNumber)) dict["tracking_number"] = trackingNumber;
        if (!string.IsNullOrEmpty(shippingProvider)) dict["shipping_provider"] = shippingProvider;

        if (receivedAt.HasValue) dict["received_at"] = receivedAt;
        if (receivedBy.HasValue) dict["received_by"] = receivedBy;
        if (!string.IsNullOrEmpty(receivingNotes)) dict["receiving_notes"] = receivingNotes;

        var json = JsonSerializer.SerializeToUtf8Bytes(dict, JsonOptions);
        return JsonDocument.Parse(json);
    }

    private static DateTime? GetLogisticsDate(JsonDocument? doc, string key)
    {
        if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Object) return null;
        
        if (doc.RootElement.TryGetProperty(key, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.String && DateTime.TryParse(prop.GetString(), out var date)) return date;
            if (prop.ValueKind == JsonValueKind.String) return null;
        }
        return null;
    }
}
