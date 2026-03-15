// decorativeplant-be.Application/Features/Branch/Handlers/GetBranchByIdQueryHandler.cs

using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Branch.DTOs;
using decorativeplant_be.Application.Features.Branch.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.Branch.Handlers;

public class GetBranchByIdQueryHandler : IRequestHandler<GetBranchByIdQuery, BranchDto>
{
    private readonly IApplicationDbContext _context;

    public GetBranchByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BranchDto> Handle(GetBranchByIdQuery request, CancellationToken cancellationToken)
    {
        var branch = await _context.Branches
            .Include(b => b.Company)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

        if (branch == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Branch), request.Id);
        }

        return branch.ToDto(branch.Company.Name);
    }
}
