using System.Text.Json;

namespace decorativeplant_be.Application.DTOs.IoT;

public class AutomationRuleDto
{
    public Guid Id { get; set; }
    public Guid? DeviceId { get; set; }
    public string? Name { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public JsonDocument? Schedule { get; set; }
    public JsonDocument? Conditions { get; set; }
    public JsonDocument? Actions { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class CreateAutomationRuleDto
{
    public Guid? DeviceId { get; set; }
    public string? Name { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public JsonDocument? Schedule { get; set; }
    public JsonDocument? Conditions { get; set; }
    public JsonDocument? Actions { get; set; }
}

public class UpdateAutomationRuleDto
{
    public Guid? DeviceId { get; set; }
    public string? Name { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public JsonDocument? Schedule { get; set; }
    public JsonDocument? Conditions { get; set; }
    public JsonDocument? Actions { get; set; }
}
