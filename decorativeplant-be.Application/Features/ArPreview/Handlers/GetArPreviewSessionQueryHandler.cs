using decorativeplant_be.Application.Common.DTOs.ArPreview;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace decorativeplant_be.Application.Features.ArPreview.Handlers;

public class GetArPreviewSessionQueryHandler : IRequestHandler<Queries.GetArPreviewSessionQuery, ArPreviewSessionResponse?>
{
    private readonly IApplicationDbContext _context;
    private readonly IArPreviewTokenService _tokenService;

    public GetArPreviewSessionQueryHandler(IApplicationDbContext context, IArPreviewTokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    public async Task<ArPreviewSessionResponse?> Handle(Queries.GetArPreviewSessionQuery query, CancellationToken cancellationToken)
    {
        var session = await _context.ArPreviewSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == query.SessionId, cancellationToken);

        if (session == null) return null;
        if (DateTime.UtcNow > session.ExpiresAt) return null;

        if (!_tokenService.ValidateViewerToken(session.Id, query.ViewerToken, session.TokenSalt, DateTime.UtcNow, session.ExpiresAt))
            return null;

        var scanOut = JsonDocument.Parse((session.ScanJson ?? JsonDocument.Parse("{}")).RootElement.GetRawText());

        return new ArPreviewSessionResponse
        {
            SessionId = session.Id,
            ExpiresAt = session.ExpiresAt,
            Scan = scanOut,
            ViewerToken = query.ViewerToken
        };
    }
}

