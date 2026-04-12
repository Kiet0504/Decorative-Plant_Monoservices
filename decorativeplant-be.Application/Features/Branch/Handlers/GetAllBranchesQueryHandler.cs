// decorativeplant-be.Application/Features/Branch/Handlers/GetAllBranchesQueryHandler.cs

using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Branch.DTOs;
using decorativeplant_be.Application.Features.Branch.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.Branch.Handlers;

public class GetAllBranchesQueryHandler : IRequestHandler<GetAllBranchesQuery, List<BranchDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllBranchesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<BranchDto>> Handle(GetAllBranchesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Branches
            .Include(b => b.Company)
            .AsNoTracking();

        if (request.OnlyActive)
        {
            query = query.Where(b => b.IsActive);
        }

        var branches = await query
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);

        return branches.Select(b => b.ToDto(b.Company.Name)).ToList();
    }
}
