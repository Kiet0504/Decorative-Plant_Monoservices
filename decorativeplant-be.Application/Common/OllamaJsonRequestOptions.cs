namespace decorativeplant_be.Application.Common;

/// <summary>Optional overrides for a single Ollama JSON chat call (e.g. small model + short timeout for intent classification).</summary>
public sealed class OllamaJsonRequestOptions
{
    /// <summary>When set, replaces the configured Ollama model name for this request only.</summary>
    public string? Model { get; init; }

    /// <summary>When set, replaces the configured timeout (seconds) for this request only.</summary>
    public int? TimeoutSeconds { get; init; }

    /// <summary>When true, connection/parse failures are logged at Warning instead of Error (for optional sub-calls like intent).</summary>
    public bool LogFailuresAsWarnings { get; init; }

    /// <summary>Passed to Ollama as <c>options.temperature</c> (e.g. 0 for deterministic JSON intent).</summary>
    public float? Temperature { get; init; }
}
