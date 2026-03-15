// decorativeplant-be.Application/Features/Branch/Commands/DeactivateBranchCommand.cs

using MediatR;

namespace decorativeplant_be.Application.Features.Branch.Commands;

public record DeactivateBranchCommand : IRequest<Unit>
{
    public Guid Id { get; init; }
}
