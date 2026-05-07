using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.AiChat;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.AiChat.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.AiChat.Handlers;

public sealed class AppendAiChatMessageCommandHandler : IRequestHandler<AppendAiChatMessageCommand, AiChatAppendMessageResultDto>
{
    private readonly IApplicationDbContext _db;

    public AppendAiChatMessageCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AiChatAppendMessageResultDto> Handle(AppendAiChatMessageCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty) throw new BadRequestException("User ID is required.");
        if (request.Request == null) throw new BadRequestException("Request is required.");
        if (request.Request.ThreadId == Guid.Empty) throw new BadRequestException("ThreadId is required.");

        var role = (request.Request.Role ?? "").Trim().ToLowerInvariant();
        if (role is not "assistant" and not "user")
        {
            throw new BadRequestException("Role must be 'user' or 'assistant'.");
        }

        var thread = await _db.AiChatThreads.FirstOrDefaultAsync(
            t => t.Id == request.Request.ThreadId && t.UserId == request.UserId,
            cancellationToken);
        if (thread == null) throw new NotFoundException("Chat thread", request.Request.ThreadId);

        var now = DateTime.UtcNow;
        var msg = new AiChatMessage
        {
            ThreadId = thread.Id,
            Role = role,
            Content = request.Request.Content ?? string.Empty,
            CreatedAt = now,
            AttachmentUrl = string.IsNullOrWhiteSpace(request.Request.AttachmentUrl) ? null : request.Request.AttachmentUrl!.Trim(),
            AttachmentMimeType = string.IsNullOrWhiteSpace(request.Request.AttachmentMimeType) ? null : request.Request.AttachmentMimeType!.Trim(),
            RecommendationsJson = SerializeToDocument(request.Request.NewRecommendations)
        };

        _db.AiChatMessages.Add(msg);
        thread.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        return new AiChatAppendMessageResultDto
        {
            SavedMessage = new AiChatHistoryMessageDto
            {
                Id = msg.Id,
                Role = msg.Role,
                Content = msg.Content,
                CreatedAt = msg.CreatedAt,
                AttachmentUrl = msg.AttachmentUrl,
                AttachmentMimeType = msg.AttachmentMimeType,
                NewRecommendations = request.Request.NewRecommendations
            }
        };
    }

    private static JsonDocument? SerializeToDocument<T>(T? value)
    {
        if (value == null) return null;
        try
        {
            return JsonDocument.Parse(JsonSerializer.Serialize(value));
        }
        catch
        {
            return null;
        }
    }
}

