// decorativeplant-be.Application/Features/Branch/Handlers/GetBranchesByStaffQueryHandler.cs

using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Branch.DTOs;
using decorativeplant_be.Application.Features.Branch.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.Branch.Handlers;

public class GetBranchesByStaffQueryHandler : IRequestHandler<GetBranchesByStaffQuery, List<BranchDto>>
{
    private readonly IApplicationDbContext _context;

    public GetBranchesByStaffQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<BranchDto>> Handle(GetBranchesByStaffQuery request, CancellationToken cancellationToken)
    {
        var staffAssignments = await _context.StaffAssignments
            .Include(sa => sa.Branch)
                .ThenInclude(b => b.Company)
            .AsNoTracking()
            .Where(sa => sa.StaffId == request.StaffId)
            .ToListAsync(cancellationToken);

        return staffAssignments
            .Select(sa => sa.Branch.ToDto(sa.Branch.Company.Name))
            .ToList();
    }
}
