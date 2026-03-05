using System.Text.Json;

namespace decorativeplant_be.Application.DTOs.Branch;

public class BranchDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? BranchType { get; set; }
    public JsonDocument? ContactInfo { get; set; }
    public JsonDocument? OperatingHours { get; set; }
    public JsonDocument? Settings { get; set; }
    public bool IsActive { get; set; }
}

public class CreateBranchDto
{
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? BranchType { get; set; }
    public JsonDocument? ContactInfo { get; set; }
    public JsonDocument? OperatingHours { get; set; }
    public JsonDocument? Settings { get; set; }
    public bool IsActive { get; set; } = true;
}
