// decorativeplant-be.Application/Features/Company/Handlers/DeactivateCompanyCommandHandler.cs

using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Company.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.Company.Handlers;

public class DeactivateCompanyCommandHandler : IRequestHandler<DeactivateCompanyCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public DeactivateCompanyCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(DeactivateCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = await _context.Companies
            .Include(c => c.Branches)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (company == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Company), request.Id);
        }

        // Deactivate the company (soft delete)
        company.IsDeleted = true;
        company.UpdatedAt = DateTime.UtcNow;

        // Deactivate all branches
        foreach (var branch in company.Branches)
        {
            branch.IsActive = false;
            branch.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
