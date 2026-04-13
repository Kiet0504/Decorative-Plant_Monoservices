namespace decorativeplant_be.Application.Common.DTOs.Garden;

public sealed class AiSchedulePlanDto
{
    public List<CareScheduleTaskInfoDto> Tasks { get; set; } = new();
    public string Confidence { get; set; } = "medium";
    public string? Model { get; set; }
    public DateTime? GeneratedAtUtc { get; set; }
    public List<string> Notes { get; set; } = new();
}

