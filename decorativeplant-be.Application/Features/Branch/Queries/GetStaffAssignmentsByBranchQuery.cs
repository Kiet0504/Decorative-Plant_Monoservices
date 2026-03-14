// decorativeplant-be.Application/Features/Branch/Queries/GetStaffAssignmentsByBranchQuery.cs

using decorativeplant_be.Application.Features.Branch.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Branch.Queries;

public record GetStaffAssignmentsByBranchQuery : IRequest<List<StaffAssignmentDto>>
{
    public Guid BranchId { get; init; }
}
