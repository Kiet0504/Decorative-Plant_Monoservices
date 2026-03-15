// decorativeplant-be.Application/Features/Branch/Queries/GetBranchesByCompanyQuery.cs

using decorativeplant_be.Application.Features.Branch.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Branch.Queries;

public record GetBranchesByCompanyQuery : IRequest<List<BranchDto>>
{
    public Guid CompanyId { get; init; }
    public bool? OnlyActive { get; init; }
}
