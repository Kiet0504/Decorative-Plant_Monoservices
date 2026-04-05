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
        string? speciesName = entity.Taxonomy?.ScientificName;

        if (entity.SourceInfo != null)
        {
            try 
            { 
                sourceInfo = JsonSerializer.Deserialize<object>(entity.SourceInfo.RootElement.GetRawText(), JsonOptions);
                
                // Fallback for speciesName if Taxonomy is null
                if (string.IsNullOrEmpty(speciesName) && entity.SourceInfo.RootElement.TryGetProperty("scientific_name", out var prop))
                {
                    speciesName = prop.GetString();
                }
            } catch {}
        }

        object? specs = null;
        if (entity.Specs != null)
        {
            try { specs = JsonSerializer.Deserialize<object>(entity.Specs.RootElement.GetRawText(), JsonOptions); } catch {}
        }

        return new PlantBatchDto
        {
            Id = entity.Id,
            BatchCode = entity.BatchCode,
            ParentBatchId = entity.ParentBatchId,
            ParentBatchCode = entity.ParentBatch?.BatchCode,
            BranchId = entity.BranchId,
            TaxonomyId = entity.TaxonomyId,
            SpeciesName = speciesName,
            SupplierId = entity.SupplierId,
            SupplierName = entity.Supplier?.Name,
            SourceInfo = sourceInfo,
            Specs = specs,
            InitialQuantity = entity.InitialQuantity ?? 0,
            CurrentTotalQuantity = entity.CurrentTotalQuantity ?? 0,
            CreatedAt = entity.CreatedAt
        };
    }

    public static PlantBatchSummaryDto ToSummaryDto(PlantBatch entity)
    {
        string? speciesName = entity.Taxonomy?.ScientificName;

        if (string.IsNullOrEmpty(speciesName) && entity.SourceInfo != null)
        {
            if (entity.SourceInfo.RootElement.TryGetProperty("scientific_name", out var prop))
            {
                speciesName = prop.GetString();
            }
        }

        return new PlantBatchSummaryDto
        {
            Id = entity.Id,
            BatchCode = entity.BatchCode,
            SpeciesName = speciesName,
            CurrentTotalQuantity = entity.CurrentTotalQuantity ?? 0,
            Stage = "Stable", // Default placeholder, will be refined in handler
            HealthStatus = "Resolved", // Default placeholder, will be refined in handler
            CreatedAt = entity.CreatedAt
        };
    }

    public static JsonDocument? BuildJson(object? data)
    {
        if (data == null) return null;
        return JsonSerializer.SerializeToDocument(data, JsonOptions);
    }
}
