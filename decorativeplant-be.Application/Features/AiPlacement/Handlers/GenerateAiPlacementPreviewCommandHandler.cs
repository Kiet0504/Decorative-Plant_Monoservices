using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.AiPlacement;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.AiPlacement.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.AiPlacement.Handlers;

public sealed class GenerateAiPlacementPreviewCommandHandler : IRequestHandler<GenerateAiPlacementPreviewCommand, AiPlacementPreviewResultDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IAiPlacementPreviewGenerator _generator;

    public GenerateAiPlacementPreviewCommandHandler(
        IApplicationDbContext db,
        IAiPlacementPreviewGenerator generator)
    {
        _db = db;
        _generator = generator;
    }

    public async Task<AiPlacementPreviewResultDto> Handle(GenerateAiPlacementPreviewCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty) throw new BadRequestException("User ID is required.");
        if (request.Request == null) throw new BadRequestException("Request is required.");
        if (string.IsNullOrWhiteSpace(request.Request.RoomImageBase64))
            throw new BadRequestException("RoomImageBase64 is required.");
        if (request.Request.ListingId == Guid.Empty)
            throw new BadRequestException("ListingId is required.");

        // Ensure listing exists; pull a small bit of context for prompt grounding.
        var listing = await _db.ProductListings
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Request.ListingId, cancellationToken);
        if (listing == null)
        {
            throw new NotFoundException("Product listing", request.Request.ListingId);
        }

        // Inject minimal listing info into the request for generator (keeps generator interface stable).
        // We store it inside UserNotes as a structured appendix so generator can stay stateless.
        var title = listing.ProductInfo != null &&
                    listing.ProductInfo.RootElement.TryGetProperty("title", out var t) &&
                    t.ValueKind == JsonValueKind.String
            ? t.GetString()
            : null;

        var safeTitle = string.IsNullOrWhiteSpace(title) ? "a Decorative Plant shop listing" : title!.Trim();
        var appendix = $"[ListingTitle:{safeTitle}]";
        request.Request.UserNotes = string.IsNullOrWhiteSpace(request.Request.UserNotes)
            ? appendix
            : request.Request.UserNotes.Trim() + "\n" + appendix;

        return await _generator.GenerateAsync(request.Request, cancellationToken);
    }
}

