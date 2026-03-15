// decorativeplant-be.Application/Features/Branch/Handlers/GetBranchesByCompanyQueryHandler.cs

using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Branch.DTOs;
using decorativeplant_be.Application.Features.Branch.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.Branch.Handlers;

public class GetBranchesByCompanyQueryHandler : IRequestHandler<GetBranchesByCompanyQuery, List<BranchDto>>
{
    private readonly IApplicationDbContext _context;

    public GetBranchesByCompanyQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<BranchDto>> Handle(GetBranchesByCompanyQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Branches
            .Include(b => b.Company)
            .AsNoTracking()
            .Where(b => b.CompanyId == request.CompanyId);

        // Optionally filter by IsActive
        if (request.OnlyActive.HasValue)
        {
            query = query.Where(b => b.IsActive == request.OnlyActive.Value);
        }

        var branches = await query
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);

        return branches.Select(b => b.ToDto(b.Company.Name)).ToList();
    }
}
