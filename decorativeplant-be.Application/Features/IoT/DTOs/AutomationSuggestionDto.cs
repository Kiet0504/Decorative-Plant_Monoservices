namespace decorativeplant_be.Application.Features.IoT.DTOs;

public class AutomationSuggestionDto
{
    public string PlantName { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public List<SuggestedConditionDto> Conditions { get; set; } = new();
}

public class SuggestedConditionDto
{
    public string ComponentKey { get; set; } = string.Empty;
    public string Operator { get; set; } = ">";
    public double Threshold { get; set; }
    public string Description { get; set; } = string.Empty;
    public SuggestedActionDto? SuggestedAction { get; set; }
}

public class SuggestedActionDto
{
    public string TargetComponentKey { get; set; } = string.Empty;
    public string ActionType { get; set; } = "turn_on";
    public string Value { get; set; } = "true";
}
