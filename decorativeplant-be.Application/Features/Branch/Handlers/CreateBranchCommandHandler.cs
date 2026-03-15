// decorativeplant-be.Application/Features/Branch/Handlers/CreateBranchCommandHandler.cs

using System.Text.Json;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Branch.Commands;
using decorativeplant_be.Application.Features.Branch.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.Branch.Handlers;

public class CreateBranchCommandHandler : IRequestHandler<CreateBranchCommand, BranchDto>
{
    private readonly IApplicationDbContext _context;

    public CreateBranchCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BranchDto> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify CompanyId exists
        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.Id == request.CompanyId, cancellationToken);

        if (company == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Company), request.CompanyId);
        }

        // 2. Check duplicate Code
        var codeExists = await _context.Branches
            .AnyAsync(b => b.Code == request.Code, cancellationToken);

        if (codeExists)
        {
            throw new InvalidOperationException($"Branch with code '{request.Code}' already exists.");
        }

        // 3. Create Branch with JSONB pattern
        var branch = new Domain.Entities.Branch
        {
            Id = Guid.NewGuid(),
            CompanyId = request.CompanyId,
            Code = request.Code,
            Name = request.Name,
            Slug = request.Slug,
            BranchType = request.BranchType,
            ContactInfo = JsonSerializer.SerializeToDocument(new
            {
                phone = request.ContactPhone,
                email = request.ContactEmail,
                full_address = request.FullAddress,
                city = request.City,
                lat = request.Lat,
                @long = request.Long
            }),
            OperatingHours = request.OperatingHours,
            Settings = JsonSerializer.SerializeToDocument(new
            {
                supports_online_order = request.SupportsOnlineOrder,
                supports_pickup = request.SupportsPickup,
                supports_shipping = request.SupportsShipping
            }),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Branches.Add(branch);
        await _context.SaveChangesAsync(cancellationToken);

        return branch.ToDto(company.Name);
    }
}
