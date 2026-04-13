using decorativeplant_be.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Infrastructure.Services;

public sealed class ChatDiagnosisPipelineSettings : IChatDiagnosisPipelineSettings
{
    public ChatDiagnosisPipelineSettings(IOptions<AiDiagnosisSettings> options)
    {
        var v = options.Value;
        CanRunFormalGeminiOllamaFromChat =
            string.Equals(v.Provider, "GeminiOllama", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(v.GeminiApiKey);
    }

    public bool CanRunFormalGeminiOllamaFromChat { get; }
}
