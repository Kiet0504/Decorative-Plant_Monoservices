using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

namespace decorativeplant_be.Application.Features.Commerce.Returns.Commands;

public class CreateReturnRequestCommand : IRequest<ReturnRequestResponse>
{
    public Guid UserId { get; set; }
    public CreateReturnRequestRequest Request { get; set; } = null!;
}

public class UpdateReturnStatusCommand : IRequest<ReturnRequestResponse>
{
    public Guid Id { get; set; }
    public Guid? ActorUserId { get; set; }
    public UpdateReturnStatusRequest Request { get; set; } = null!;
}
