// decorativeplant-be.Application/Features/Company/Handlers/UpdateCompanyCommandHandler.cs

using System.Text.Json;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Company.Commands;
using decorativeplant_be.Application.Features.Company.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.Company.Handlers;

public class UpdateCompanyCommandHandler : IRequestHandler<UpdateCompanyCommand, CompanyDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateCompanyCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CompanyDto> Handle(UpdateCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.Id == request.Id && !c.IsDeleted, cancellationToken);

        if (company == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Company), request.Id);
        }

        // Update scalar fields
        company.Name = request.Name;
        company.TaxCode = request.TaxCode;
        company.Email = request.Email;
        company.Phone = request.Phone;

        // Rebuild Info jsonb entirely
        company.Info = JsonSerializer.SerializeToDocument(new
        {
            logo_url = request.LogoUrl,
            website = request.Website,
            description = request.Description,
            address = request.Address
        });

        company.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return company.ToDto();
    }
}
