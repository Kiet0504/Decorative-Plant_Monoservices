using System.Text.Json;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Inventory;

public static class PlantBatchMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public static PlantBatchDto ToDto(PlantBatch entity)
    {
        object? sourceInfo = null;
        if (entity.SourceInfo != null)
        {
            try { sourceInfo = JsonSerializer.Deserialize<object>(entity.SourceInfo.RootElement.GetRawText(), JsonOptions); } catch {}
        }

        object? specs = null;
        if (entity.Specs != null)
        {
            try { specs = JsonSerializer.Deserialize<object>(entity.Specs.RootElement.GetRawText(), JsonOptions); } catch {}
        }

        var dto = new PlantBatchDto
        {
            Id = entity.Id,
            BatchCode = entity.BatchCode,
            ParentBatchId = entity.ParentBatchId,
            ParentBatchCode = entity.ParentBatch?.BatchCode,
            BranchId = entity.BranchId,
            BranchName = entity.Branch?.Name,
            TaxonomyId = entity.TaxonomyId,
            SpeciesName = GetSpeciesDisplayName(entity.Taxonomy),
            SupplierId = entity.SupplierId,
            SupplierName = entity.Supplier?.Name,
            SourceInfo = sourceInfo,
            Specs = specs,
            HealthStatus = NormalizeValue(ExtractSpec(entity.Specs, "health_status") ?? "Healthy"),
            Stage = NormalizeValue(ExtractSpec(entity.Specs, "maturity_stage") ?? "Stable"),
            InitialQuantity = entity.InitialQuantity ?? 0,
            CurrentTotalQuantity = entity.CurrentTotalQuantity ?? 0,
            PurchaseCost = ExtractSourceCost(entity.SourceInfo),
            ImageUrl = entity.Taxonomy?.ImageUrl,
            CreatedAt = entity.CreatedAt,
            CategoryName = entity.Taxonomy?.Category?.Slug ?? entity.Taxonomy?.Category?.Name
        };

        // Populate Location Info from first non-sales stock
        var primaryStock = entity.BatchStocks?.FirstOrDefault(bs => 
            bs.Location?.Type != "Sales" && bs.Location?.Type != "Storefront");
        
        if (primaryStock != null)
        {
            dto.LocationId = primaryStock.LocationId;
            dto.LocationName = primaryStock.Location?.Name;
        }
        else if (entity.BatchStocks != null && entity.BatchStocks.Any())
        {
            // Fallback to first stock if no non-sales stock found
            var fallback = entity.BatchStocks.First();
            dto.LocationId = fallback.LocationId;
            dto.LocationName = fallback.Location?.Name;
        }

        // Populate all non-sales locations for multi-location display
        if (entity.BatchStocks != null)
        {
            var cultivationStocks = entity.BatchStocks
                .Where(bs => bs.Location?.Type != "Sales" && bs.Location?.Type != "Storefront")
                .ToList();
            
            if (cultivationStocks.Count > 0)
            {
                dto.Locations = cultivationStocks
                    .Select(bs => new BatchLocationDto
                    {
                        LocationId = bs.LocationId,
                        LocationName = bs.Location?.Name,
                        Quantity = ExtractStockQuantity(bs)
                    })
                    .Where(x => x.Quantity > 0) // Only show locations that actually host plants
                    .ToList();
            }
        }

        // Populate Aggregate Stock Fields
        if (entity.BatchStocks != null && entity.BatchStocks.Any())
        {
            var aggregated = AggregateStock(entity.BatchStocks);
            dto.Quantity = aggregated.quantity;
            dto.ReservedQuantity = aggregated.reserved;
            dto.AvailableQuantity = aggregated.available;
            dto.TotalReceived = aggregated.totalReceived;
        }

        return dto;
    }

    public static PlantBatchSummaryDto ToSummaryDto(PlantBatch entity)
    {
        var dto = new PlantBatchSummaryDto
        {
            Id = entity.Id,
            BatchCode = entity.BatchCode,
            SpeciesName = GetSpeciesDisplayName(entity.Taxonomy),
            BranchName = entity.Branch?.Name, // Added
            HealthStatus = NormalizeValue(ExtractSpec(entity.Specs, "health_status") ?? "Healthy"),
            Stage = NormalizeValue(ExtractSpec(entity.Specs, "maturity_stage") ?? "Stable"),
            InitialQuantity = entity.InitialQuantity ?? 0,
            CurrentTotalQuantity = entity.CurrentTotalQuantity ?? 0,
            CreatedAt = entity.CreatedAt
        };

        // Populate Aggregate Stock Fields for Summary
        if (entity.BatchStocks != null && entity.BatchStocks.Any())
        {
            var aggregated = AggregateStock(entity.BatchStocks);
            dto.Quantity = aggregated.quantity;
            dto.ReservedQuantity = aggregated.reserved;
            dto.AvailableQuantity = aggregated.available;
            dto.TotalReceived = aggregated.totalReceived;
        }

        return dto;
    }

    private static string NormalizeValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        
        // Handle snake_case or kebab-case
        value = value.Replace("_", " ").Replace("-", " ");
        
        // Capitalize each word
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
                words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
        }
        
        return string.Join(" ", words);
    }

    private static string? ExtractSpec(JsonDocument? specs, string key)
    {
        if (specs == null) return null;
        try
        {
            if (specs.RootElement.TryGetProperty(key, out var prop))
            {
                return prop.GetString();
            }
        }
        catch { }
        return null;
    }

    private static decimal? ExtractSourceCost(JsonDocument? sourceInfo)
    {
        if (sourceInfo == null) return null;
        try
        {
            if (sourceInfo.RootElement.TryGetProperty("purchase_cost", out var prop))
            {
                if (prop.TryGetDecimal(out var cost)) return cost;
                // Sometimes numbers come in as strings from bad clients
                if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), out var strCost)) return strCost;
            }
        }
        catch { }
        return null;
    }

    private static string? GetSpeciesDisplayName(PlantTaxonomy? taxonomy)
    {
        if (taxonomy == null) return null;
        
        if (taxonomy.CommonNames != null)
        {
            try
            {
                if (taxonomy.CommonNames.RootElement.TryGetProperty("en", out var enProp))
                {
                    var enName = enProp.GetString();
                    if (!string.IsNullOrEmpty(enName)) return enName;
                }
                if (taxonomy.CommonNames.RootElement.TryGetProperty("vi", out var viProp))
                {
                    if (viProp.ValueKind == JsonValueKind.Array && viProp.GetArrayLength() > 0)
                        return viProp.EnumerateArray().First().GetString();
                    
                    var viName = viProp.GetString();
                    if (!string.IsNullOrEmpty(viName)) return viName;
                }
            }
            catch { }
        }

        return taxonomy.ScientificName;
    }

    public static JsonDocument? BuildJson(object? data)
    {
        if (data == null) return null;
        return JsonSerializer.SerializeToDocument(data, JsonOptions);
    }

    private static (int quantity, int reserved, int available, int totalReceived) AggregateStock(IEnumerable<BatchStock> stocks)
    {
        int q = 0, r = 0, a = 0, tr = 0;
        foreach (var stock in stocks)
        {
            if (stock.Quantities == null) continue;
            try
            {
                var json = stock.Quantities.RootElement;
                if (json.TryGetProperty("quantity", out var qProp) && qProp.ValueKind == JsonValueKind.Number) q += (int)qProp.GetDouble();
                if (json.TryGetProperty("reserved_quantity", out var rProp) && rProp.ValueKind == JsonValueKind.Number) r += (int)rProp.GetDouble();
                if (json.TryGetProperty("available_quantity", out var aProp) && aProp.ValueKind == JsonValueKind.Number) a += (int)aProp.GetDouble();
                if (json.TryGetProperty("total_received", out var trProp) && trProp.ValueKind == JsonValueKind.Number) tr += (int)trProp.GetDouble();
            }
            catch { }
        }
        return (q, r, a, tr);
    }

    private static int ExtractStockQuantity(BatchStock stock)
    {
        if (stock.Quantities == null) return 0;
        try
        {
            // The UI expects 'current cultivation stock' for the location labels.
            // According to the new model, this is 'reserved_quantity'.
            if (stock.Quantities.RootElement.TryGetProperty("reserved_quantity", out var qProp) 
                && qProp.ValueKind == JsonValueKind.Number)
                return (int)qProp.GetDouble();
        }
        catch { }
        return 0;
    }
}
