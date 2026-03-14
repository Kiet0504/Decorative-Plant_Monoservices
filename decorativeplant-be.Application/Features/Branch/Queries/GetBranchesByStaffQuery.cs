// decorativeplant-be.Application/Features/Branch/Queries/GetBranchesByStaffQuery.cs

using decorativeplant_be.Application.Features.Branch.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Branch.Queries;

public record GetBranchesByStaffQuery : IRequest<List<BranchDto>>
{
    public Guid StaffId { get; init; }
}
