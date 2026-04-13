using decorativeplant_be.Application.Common.DTOs.ArPreview;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Common.Security;
using decorativeplant_be.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace decorativeplant_be.Application.Features.ArPreview.Handlers;

public class CreateArPreviewSessionCommandHandler : IRequestHandler<Commands.CreateArPreviewSessionCommand, ArPreviewSessionResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IArPreviewTokenService _tokenService;

    public CreateArPreviewSessionCommandHandler(IApplicationDbContext context, IArPreviewTokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    public async Task<ArPreviewSessionResponse> Handle(Commands.CreateArPreviewSessionCommand command, CancellationToken cancellationToken)
    {
        // TTL: keep short for MVP
        var expiresAt = DateTime.UtcNow.AddMinutes(30);

        // Compose scan json: include placement/product hint if provided
        JsonDocument scan = command.Request.Scan;
        if (command.Request.Placement != null || command.Request.ProductListingId != null)
        {
            using var doc = JsonDocument.Parse("{}");
            // merge by constructing a small wrapper object so we don't need a deep merge lib
            var wrapper = new
            {
                scan = scan.RootElement,
                placement = command.Request.Placement?.RootElement,
                productListingId = command.Request.ProductListingId
            };
            scan = JsonDocument.Parse(JsonSerializer.Serialize(wrapper));
        }

        var salt = _tokenService.CreateSalt();
        var session = new ArPreviewSession
        {
            UserId = command.UserId,
            ScanJson = scan,
            ExpiresAt = expiresAt,
            TokenSalt = salt,
            CreatedAt = DateTime.UtcNow
        };

        _context.ArPreviewSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        var expUnix = new DateTimeOffset(expiresAt).ToUnixTimeSeconds();
        var token = _tokenService.CreateViewerToken(session.Id, expUnix, salt);

        // prevent EF tracking surprises with JsonDocument disposal later
        var scanOut = JsonDocument.Parse((session.ScanJson ?? JsonDocument.Parse("{}")).RootElement.GetRawText());

        return new ArPreviewSessionResponse
        {
            SessionId = session.Id,
            ExpiresAt = expiresAt,
            Scan = scanOut,
            ViewerToken = token
        };
    }
}

