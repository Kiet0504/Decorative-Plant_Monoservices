// decorativeplant-be.Application/Features/Company/CompanyMapper.cs

using decorativeplant_be.Application.Features.Company.DTOs;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Company;

public static class CompanyMapper
{
    public static CompanyDto ToDto(this Domain.Entities.Company c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        TaxCode = c.TaxCode,
        Email = c.Email,
        Phone = c.Phone,
        LogoUrl = c.Info?.RootElement.TryGetProperty("logo_url", out var p1) == true
            ? p1.GetString() : null,
        Website = c.Info?.RootElement.TryGetProperty("website", out var p2) == true
            ? p2.GetString() : null,
        Description = c.Info?.RootElement.TryGetProperty("description", out var p3) == true
            ? p3.GetString() : null,
        Address = c.Info?.RootElement.TryGetProperty("address", out var p4) == true
            ? p4.GetString() : null,
        CreatedAt = c.CreatedAt
    };
}
