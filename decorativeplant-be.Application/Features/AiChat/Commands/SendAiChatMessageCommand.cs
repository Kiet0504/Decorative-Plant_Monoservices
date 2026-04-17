using decorativeplant_be.Application.Common.DTOs.AiChat;
using MediatR;

namespace decorativeplant_be.Application.Features.AiChat.Commands;

public sealed class SendAiChatMessageCommand : IRequest<AiChatReplyDto>
{
    public Guid UserId { get; set; }
    public List<AiChatMessageDto> Messages { get; set; } = new();
    public Guid? GardenPlantId { get; set; }

    public string? AttachedImageBase64 { get; set; }
    public string? AttachedImageMimeType { get; set; }

    public RoomScanChatFollowUpDto? RoomScanFollowUp { get; set; }
}
