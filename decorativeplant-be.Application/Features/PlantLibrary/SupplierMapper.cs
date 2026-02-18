using System.Text.Json;
using decorativeplant_be.Application.Features.PlantLibrary.DTOs;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.PlantLibrary;

public static class SupplierMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public static SupplierDto ToDto(Supplier supplier)
    {
        string? address = null;
        // Extract address from contact_info JSONB
        if (supplier.ContactInfo != null && supplier.ContactInfo.RootElement.ValueKind == JsonValueKind.Object)
        {
             if (supplier.ContactInfo.RootElement.TryGetProperty("address", out var addrProp) && addrProp.ValueKind == JsonValueKind.String)
             {
                 address = addrProp.GetString();
             }
        }

        return new SupplierDto
        {
            Id = supplier.Id,
            Name = supplier.Name ?? string.Empty,
            Address = address,
            ContactInfo = supplier.ContactInfo?.RootElement.ToString()
        };
    }

    public static JsonDocument BuildContactInfo(string? rawContactInfo, string? address)
    {
        var dict = new Dictionary<string, object?>();
        if (!string.IsNullOrEmpty(rawContactInfo)) dict["raw"] = rawContactInfo;
        if (!string.IsNullOrEmpty(address)) dict["address"] = address;
        
        var json = JsonSerializer.SerializeToUtf8Bytes(dict, JsonOptions);
        return JsonDocument.Parse(json);
    }
}
