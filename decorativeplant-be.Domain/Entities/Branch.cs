using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class BranchContactInfo
{
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? FullAddress { get; set; }
    public string? City { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class Branch : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? BranchType { get; set; }
    
    // JSONB
    public BranchContactInfo? ContactInfo { get; set; }
    public JsonNode? OperatingHours { get; set; }
    public JsonNode? Settings { get; set; }
    
    public bool IsActive { get; set; } = true;
}
