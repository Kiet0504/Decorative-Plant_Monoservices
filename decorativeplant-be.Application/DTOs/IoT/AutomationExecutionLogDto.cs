using System.Text.Json;

namespace decorativeplant_be.Application.DTOs.IoT;

public class AutomationExecutionLogDto
{
    public Guid Id { get; set; }
    public Guid? RuleId { get; set; }
    public JsonDocument? ExecutionInfo { get; set; }
    public DateTime? ExecutedAt { get; set; }
}
