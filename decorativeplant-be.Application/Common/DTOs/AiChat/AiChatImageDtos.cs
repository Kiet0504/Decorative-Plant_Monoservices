namespace decorativeplant_be.Application.Common.DTOs.AiChat;

public sealed class AiChatGenerateImageRequestDto
{
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Client cache key (e.g. Tropical_Living:tropical) to allow deterministic URLs.</summary>
    public string CacheKey { get; set; } = string.Empty;
}

public sealed class AiChatGenerateImageResultDto
{
    public string Url { get; set; } = string.Empty;
}

