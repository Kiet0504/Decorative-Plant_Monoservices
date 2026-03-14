// decorativeplant-be.Application/Features/Branch/Queries/GetBranchByIdQuery.cs

using decorativeplant_be.Application.Features.Branch.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Branch.Queries;

public record GetBranchByIdQuery : IRequest<BranchDto>
{
    public Guid Id { get; init; }
}
