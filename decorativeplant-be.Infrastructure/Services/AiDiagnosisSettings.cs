namespace decorativeplant_be.Infrastructure.Services;

public class AiDiagnosisSettings
{
    public const string SectionName = "AiDiagnosis";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o";
    public int MaxTokens { get; set; } = 1024;
}
