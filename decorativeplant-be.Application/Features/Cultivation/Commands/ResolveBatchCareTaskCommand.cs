using MediatR;

namespace decorativeplant_be.Application.Features.Cultivation.Commands;

public class ResolveBatchCareTaskCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public Guid PerformedBy { get; set; }
}
