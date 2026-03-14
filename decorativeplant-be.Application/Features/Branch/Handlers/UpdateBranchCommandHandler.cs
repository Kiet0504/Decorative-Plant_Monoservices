// decorativeplant-be.Application/Features/Branch/Handlers/UpdateBranchCommandHandler.cs

using System.Text.Json;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Branch.Commands;
using decorativeplant_be.Application.Features.Branch.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.Branch.Handlers;

public class UpdateBranchCommandHandler : IRequestHandler<UpdateBranchCommand, BranchDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateBranchCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BranchDto> Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        // Find + Include Company
        var branch = await _context.Branches
            .Include(b => b.Company)
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

        if (branch == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Branch), request.Id);
        }

        // Update scalar fields
        branch.Code = request.Code;
        branch.Name = request.Name;
        branch.Slug = request.Slug;
        branch.BranchType = request.BranchType;

        // Rebuild all 3 JSONB documents
        branch.ContactInfo = JsonSerializer.SerializeToDocument(new
        {
            phone = request.ContactPhone,
            email = request.ContactEmail,
            full_address = request.FullAddress,
            city = request.City,
            lat = request.Lat,
            @long = request.Long
        });

        branch.OperatingHours = request.OperatingHours;

        branch.Settings = JsonSerializer.SerializeToDocument(new
        {
            supports_online_order = request.SupportsOnlineOrder,
            supports_pickup = request.SupportsPickup,
            supports_shipping = request.SupportsShipping
        });

        branch.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return branch.ToDto(branch.Company.Name);
    }
}
