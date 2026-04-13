using decorativeplant_be.Application.Common.DTOs.ArPreview;
using MediatR;

namespace decorativeplant_be.Application.Features.ArPreview.Commands;

public class CreateArPreviewSessionCommand : IRequest<ArPreviewSessionResponse>
{
    public Guid? UserId { get; set; }
    public CreateArPreviewSessionRequest Request { get; set; } = new();
}

