using System.Text.Json;
using decorativeplant_be.Application.Features.PlantLibrary.DTOs;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.PlantLibrary;

public static class PlantTaxonomyMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public static PlantTaxonomyDto ToDto(PlantTaxonomy entity)
    {
        string? commonNameEn = null;
        string? commonNameVi = null;

        if (entity.CommonNames != null)
        {
            try 
            {
                if (entity.CommonNames.RootElement.TryGetProperty("en", out var enProp)) commonNameEn = enProp.GetString();
                if (entity.CommonNames.RootElement.TryGetProperty("vi", out var viProp)) commonNameVi = viProp.GetString();
            }
            catch { /* Ignore malformed JSON */ }
        }

        object? taxonomyInfo = null;
        if (entity.TaxonomyInfo != null)
        {
            try { taxonomyInfo = JsonSerializer.Deserialize<object>(entity.TaxonomyInfo.RootElement.GetRawText(), JsonOptions); } catch {}
        }

        object? careInfo = null;
        if (entity.CareInfo != null)
        {
            try { careInfo = JsonSerializer.Deserialize<object>(entity.CareInfo.RootElement.GetRawText(), JsonOptions); } catch {}
        }

        object? growthInfo = null;
        if (entity.GrowthInfo != null)
        {
            try { growthInfo = JsonSerializer.Deserialize<object>(entity.GrowthInfo.RootElement.GetRawText(), JsonOptions); } catch {}
        }

        return new PlantTaxonomyDto
        {
            Id = entity.Id,
            ScientificName = entity.ScientificName,
            CommonNameEn = commonNameEn,
            CommonNameVi = commonNameVi,
            TaxonomyInfo = taxonomyInfo,
            CareInfo = careInfo,
            GrowthInfo = growthInfo,
            ImageUrl = entity.ImageUrl,
            CategoryId = entity.CategoryId,
            CategoryName = entity.Category?.Name
        };
    }

    public static PlantTaxonomySummaryDto ToSummaryDto(PlantTaxonomy entity)
    {
        string? commonName = null;
        if (entity.CommonNames != null)
        {
            // Default to EN, fallback to VI
            try 
            {
                if (entity.CommonNames.RootElement.TryGetProperty("en", out var enProp)) commonName = enProp.GetString();
                if (string.IsNullOrEmpty(commonName) && entity.CommonNames.RootElement.TryGetProperty("vi", out var viProp)) commonName = viProp.GetString();
            }
            catch { }
        }

        return new PlantTaxonomySummaryDto
        {
            Id = entity.Id,
            ScientificName = entity.ScientificName,
            CommonName = commonName,
            ImageUrl = entity.ImageUrl,
            CategoryName = entity.Category?.Name
        };
    }

    public static JsonDocument BuildCommonNames(string? en, string? vi)
    {
        var dict = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(en)) dict["en"] = en;
        if (!string.IsNullOrEmpty(vi)) dict["vi"] = vi;
        
        return JsonSerializer.SerializeToDocument(dict, JsonOptions);
    }

    public static JsonDocument? BuildJson(object? data)
    {
        if (data == null) return null;
        return JsonSerializer.SerializeToDocument(data, JsonOptions);
    }
}
