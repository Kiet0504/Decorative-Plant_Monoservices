// decorativeplant-be.Application/Features/Company/Handlers/GetAllCompaniesQueryHandler.cs

using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Company.DTOs;
using decorativeplant_be.Application.Features.Company.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.Company.Handlers;

public class GetAllCompaniesQueryHandler : IRequestHandler<GetAllCompaniesQuery, List<CompanyDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllCompaniesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<CompanyDto>> Handle(GetAllCompaniesQuery request, CancellationToken cancellationToken)
    {
        var companies = await _context.Companies
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return companies.Select(c => c.ToDto()).ToList();
    }
}
