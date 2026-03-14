// decorativeplant-be.Application/Features/Company/DTOs/CompanyDto.cs

namespace decorativeplant_be.Application.Features.Company.DTOs;

public class CompanyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TaxCode { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? LogoUrl { get; set; }
    public string? Website { get; set; }
    public string? Description { get; set; }
    public string? Address { get; set; }
    public DateTime CreatedAt { get; set; }
}
