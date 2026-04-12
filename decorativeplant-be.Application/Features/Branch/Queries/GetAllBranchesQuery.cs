// decorativeplant-be.Application/Features/Branch/Queries/GetAllBranchesQuery.cs

using decorativeplant_be.Application.Features.Branch.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Branch.Queries;

public record GetAllBranchesQuery : IRequest<List<BranchDto>>
{
    public bool OnlyActive { get; init; } = true;
}
