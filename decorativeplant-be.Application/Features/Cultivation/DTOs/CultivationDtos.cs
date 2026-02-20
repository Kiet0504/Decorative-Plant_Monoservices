namespace decorativeplant_be.Application.Features.Cultivation.DTOs;

public class CultivationLogDto
{
    public Guid Id { get; set; }
    public Guid? BatchId { get; set; }
    public string? BatchCode { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public object? Details { get; set; } // JSONB
    public Guid? PerformedBy { get; set; }
    public string? PerformedByName { get; set; }
    public DateTime? PerformedAt { get; set; }
}

public class CreateCultivationLogDto
{
    public Guid? BatchId { get; set; }
    public Guid? LocationId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Dictionary<string, object>? Details { get; set; }
    public DateTime? PerformedAt { get; set; }
}
