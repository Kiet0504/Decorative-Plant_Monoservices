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

        object? embedding = null;
        if (entity.AiEmbedding != null)
        {
            try { embedding = JsonSerializer.Deserialize<object>(entity.AiEmbedding.RootElement.GetRawText(), JsonOptions); } catch {}
        }

        return new PlantBatchDto
        {
            Id = entity.Id,
            BatchCode = entity.BatchCode,
            ParentBatchId = entity.ParentBatchId,
            ParentBatchCode = entity.ParentBatch?.BatchCode,
            BranchId = entity.BranchId,
            TaxonomyId = entity.TaxonomyId,
            SpeciesName = entity.Taxonomy?.ScientificName,
            SupplierId = entity.SupplierId,
            SupplierName = entity.Supplier?.Name,
            SourceInfo = sourceInfo,
            Specs = specs,
            AiEmbedding = embedding,
            InitialQuantity = entity.InitialQuantity ?? 0,
            CurrentTotalQuantity = entity.CurrentTotalQuantity ?? 0,
            CreatedAt = entity.CreatedAt
        };
    }

    public static PlantBatchSummaryDto ToSummaryDto(PlantBatch entity)
    {
        return new PlantBatchSummaryDto
        {
            Id = entity.Id,
            BatchCode = entity.BatchCode,
            SpeciesName = entity.Taxonomy?.ScientificName,
            CurrentTotalQuantity = entity.CurrentTotalQuantity ?? 0,
            CreatedAt = entity.CreatedAt
        };
    }

    public static JsonDocument? BuildJson(object? data)
    {
        if (data == null) return null;
        return JsonSerializer.SerializeToDocument(data, JsonOptions);
    }
}
