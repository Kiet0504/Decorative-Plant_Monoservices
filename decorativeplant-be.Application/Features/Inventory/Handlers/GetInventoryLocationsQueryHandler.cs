using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Queries;
using decorativeplant_be.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class GetInventoryLocationsQueryHandler : IRequestHandler<GetInventoryLocationsQuery, IEnumerable<InventoryLocationDto>>
{
    private readonly IApplicationDbContext _context;

    public GetInventoryLocationsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<InventoryLocationDto>> Handle(GetInventoryLocationsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.InventoryLocations.AsNoTracking();

        if (request.BranchId.HasValue)
        {
            query = query.Where(x => x.BranchId == request.BranchId.Value);
        }

        var locations = await query.ToListAsync(cancellationToken);

        return locations.Select(l => new InventoryLocationDto
        {
            Id = l.Id,
            BranchId = l.BranchId,
            ParentLocationId = l.ParentLocationId,
            Code = l.Code,
            Name = l.Name,
            Type = l.Type,
            // Since DTO uses Description/Capacity but Entity uses details JSON
            // Simplified mapping for the DTO
            Description = l.Details?.RootElement.TryGetProperty("description", out var desc) == true ? desc.GetString() : null,
            Capacity = l.Details?.RootElement.TryGetProperty("capacity", out var cap) == true && cap.TryGetInt32(out var capVal) ? capVal : null
        });
    }
}
