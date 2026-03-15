// decorativeplant-be.Application/Features/Company/Handlers/CreateCompanyCommandHandler.cs

using System.Text.Json;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Company.Commands;
using decorativeplant_be.Application.Features.Company.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Company.Handlers;

public class CreateCompanyCommandHandler : IRequestHandler<CreateCompanyCommand, CompanyDto>
{
    private readonly IApplicationDbContext _context;

    public CreateCompanyCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CompanyDto> Handle(CreateCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = new Domain.Entities.Company
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            TaxCode = request.TaxCode,
            Email = request.Email,
            Phone = request.Phone,
            Info = JsonSerializer.SerializeToDocument(new
            {
                logo_url = request.LogoUrl,
                website = request.Website,
                description = request.Description,
                address = request.Address
            }),
            CreatedAt = DateTime.UtcNow
        };

        _context.Companies.Add(company);
        await _context.SaveChangesAsync(cancellationToken);

        return company.ToDto();
    }
}
