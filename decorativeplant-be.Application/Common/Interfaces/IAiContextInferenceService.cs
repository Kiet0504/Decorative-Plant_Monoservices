using decorativeplant_be.Application.Common.DTOs.AiChat;

namespace decorativeplant_be.Application.Common.Interfaces;

public interface IAiContextInferenceService
{
    Task<List<AiChatContextInferenceDto>> InferAsync(
        string userText,
        IReadOnlyList<AiChatDesignStyleOptionDto> availableStyles,
        CancellationToken cancellationToken = default);
}

