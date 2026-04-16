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

        return new PlantBatchDto
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
            ImageUrl = entity.Taxonomy?.ImageUrl,
            CreatedAt = entity.CreatedAt
        };
    }

    public static PlantBatchSummaryDto ToSummaryDto(PlantBatch entity)
    {
        return new PlantBatchSummaryDto
        {
            Id = entity.Id,
            BatchCode = entity.BatchCode,
            SpeciesName = GetSpeciesDisplayName(entity.Taxonomy),
            BranchName = entity.Branch?.Name, // Added
            HealthStatus = NormalizeValue(ExtractSpec(entity.Specs, "health_status") ?? "Healthy"),
            Stage = NormalizeValue(ExtractSpec(entity.Specs, "maturity_stage") ?? "Stable"),
            CurrentTotalQuantity = entity.CurrentTotalQuantity ?? 0,
            CreatedAt = entity.CreatedAt
        };
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
}
