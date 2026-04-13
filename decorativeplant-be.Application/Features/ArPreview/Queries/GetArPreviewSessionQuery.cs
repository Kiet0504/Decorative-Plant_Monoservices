using decorativeplant_be.Application.Common.DTOs.ArPreview;
using MediatR;

namespace decorativeplant_be.Application.Features.ArPreview.Queries;

public class GetArPreviewSessionQuery : IRequest<ArPreviewSessionResponse?>
{
    public Guid SessionId { get; set; }
    public string ViewerToken { get; set; } = string.Empty;
}

