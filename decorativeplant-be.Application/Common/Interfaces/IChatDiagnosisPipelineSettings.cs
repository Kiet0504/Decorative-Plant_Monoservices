namespace decorativeplant_be.Application.Common.Interfaces;

/// <summary>
/// Whether chat may run the formal Gemini + Ollama diagnosis pipeline on uploaded images (reads host config).
/// </summary>
public interface IChatDiagnosisPipelineSettings
{
    /// <summary>True when Provider is GeminiOllama and Gemini API key is set.</summary>
    bool CanRunFormalGeminiOllamaFromChat { get; }
}
