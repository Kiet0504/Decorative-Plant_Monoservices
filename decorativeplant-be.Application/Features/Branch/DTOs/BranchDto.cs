// decorativeplant-be.Application/Features/Branch/DTOs/BranchDto.cs

using System.Text.Json;

namespace decorativeplant_be.Application.Features.Branch.DTOs;

public class BranchDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? BranchType { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }

    // From ContactInfo jsonb
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? FullAddress { get; set; }
    public string? City { get; set; }
    public double? Lat { get; set; }
    public double? Long { get; set; }

    // OperatingHours kept as raw JsonDocument
    public JsonDocument? OperatingHours { get; set; }

    // From Settings jsonb
    public bool SupportsOnlineOrder { get; set; }
    public bool SupportsPickup { get; set; }
    public bool SupportsShipping { get; set; }
}
