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
            FromBranchName = transfer.FromBranch?.Name,
            FromBranchAddress = GetBranchAddress(transfer.FromBranch),
            ToBranchName = transfer.ToBranch?.Name,
            ToBranchAddress = GetBranchAddress(transfer.ToBranch),
            SpeciesName = GetSpeciesName(transfer.Batch),
            Quantity = transfer.Quantity,
            Status = transfer.Status ?? "requested",
            CreatedAt = transfer.CreatedAt ?? DateTime.UtcNow,
            ShippedAt = GetLogisticsDate(transfer.LogisticsInfo, "shipped_at"),
            ReceivedAt = GetLogisticsDate(transfer.LogisticsInfo, "received_at")
        };
    }

    public static JsonDocument BuildLogisticsInfo(
        object? requestedBy = null,
        string? notes = null,
        DateTime? shippedAt = null,
        object? shippedBy = null,
        string? trackingNumber = null,
        string? shippingProvider = null, 
        DateTime? receivedAt = null,
        object? receivedBy = null,
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

        if (requestedBy != null) dict["requested_by"] = requestedBy;
        if (!string.IsNullOrEmpty(notes)) dict["notes"] = notes;
        
        if (shippedAt.HasValue) dict["shipped_at"] = shippedAt;
        if (shippedBy != null) dict["shipped_by"] = shippedBy;
        if (!string.IsNullOrEmpty(trackingNumber)) dict["tracking_number"] = trackingNumber;
        if (!string.IsNullOrEmpty(shippingProvider)) dict["shipping_provider"] = shippingProvider;

        if (receivedAt.HasValue) dict["received_at"] = receivedAt;
        if (receivedBy != null) dict["received_by"] = receivedBy;
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

    private static string? GetSpeciesName(PlantBatch? batch)
    {
        if (batch?.Taxonomy == null) return null;
        
        string taxVi = "";
        string taxEn = "";
        if (batch.Taxonomy.CommonNames != null)
        {
            var root = batch.Taxonomy.CommonNames.RootElement;
            if (root.TryGetProperty("vi", out var viProp)) taxVi = viProp.GetString() ?? "";
            if (root.TryGetProperty("en", out var enProp)) taxEn = enProp.GetString() ?? "";
        }
        
        return !string.IsNullOrEmpty(taxVi) ? taxVi : (!string.IsNullOrEmpty(taxEn) ? taxEn : batch.Taxonomy.ScientificName);
    }

    private static string? GetBranchAddress(decorativeplant_be.Domain.Entities.Branch? branch)
    {
        if (branch?.ContactInfo == null) return null;
        
        try 
        {
            var root = branch.ContactInfo.RootElement;
            if (root.TryGetProperty("full_address", out var addr))
                return addr.GetString();
        }
        catch 
        {
            // Fallback
        }
        
        return null;
    }
}
