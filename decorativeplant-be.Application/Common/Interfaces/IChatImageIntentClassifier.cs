namespace decorativeplant_be.Application.Common.Interfaces;

/// <summary>
/// Decides whether the formal Gemini + Ollama disease pipeline should run for a chat turn with an attached image.
/// May use a small local LLM (Ollama) or keyword heuristics depending on configuration.
/// </summary>
public interface IChatImageIntentClassifier
{
    /// <summary>
    /// Returns true when the formal diagnosis pipeline should be used (image required; caller must still check API/config gates).
    /// </summary>
    Task<bool> ShouldUseFormalDiagnosisPipelineAsync(string? lastUserText, bool hasImage, CancellationToken cancellationToken = default);
}
