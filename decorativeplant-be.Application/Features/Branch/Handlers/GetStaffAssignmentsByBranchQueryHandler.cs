// decorativeplant-be.Application/Features/Branch/Handlers/GetStaffAssignmentsByBranchQueryHandler.cs

using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Branch.DTOs;
using decorativeplant_be.Application.Features.Branch.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.Branch.Handlers;

public class GetStaffAssignmentsByBranchQueryHandler : IRequestHandler<GetStaffAssignmentsByBranchQuery, List<StaffAssignmentDto>>
{
    private readonly IApplicationDbContext _context;

    public GetStaffAssignmentsByBranchQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<StaffAssignmentDto>> Handle(GetStaffAssignmentsByBranchQuery request, CancellationToken cancellationToken)
    {
        var staffAssignments = await _context.StaffAssignments
            .Include(sa => sa.Staff)
            .Include(sa => sa.Branch)
            .AsNoTracking()
            .Where(sa => sa.BranchId == request.BranchId)
            .ToListAsync(cancellationToken);

        return staffAssignments
            .Select(sa => sa.ToDto(sa.Staff.Email, sa.Branch.Name, sa.Staff.DisplayName))
            .ToList();
    }
}
