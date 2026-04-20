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
        var query = _context.InventoryLocations
            .Include(x => x.BatchStocks)
                .ThenInclude(bs => bs.Batch)
                    .ThenInclude(b => b!.Taxonomy)
            .AsNoTracking();

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
            Description = l.Details?.RootElement.TryGetProperty("description", out var desc) == true ? desc.GetString() : null,
            Capacity = l.Details?.RootElement.TryGetProperty("capacity", out var cap) == true && cap.TryGetInt32(out var capVal) ? capVal : null,
            EnvironmentType = l.Details?.RootElement.TryGetProperty("environment_type", out var env) == true ? env.GetString() : null,
            PositionX = l.Details?.RootElement.TryGetProperty("position_x", out var posX) == true && posX.TryGetDouble(out var posXVal) ? posXVal : null,
            PositionY = l.Details?.RootElement.TryGetProperty("position_y", out var posY) == true && posY.TryGetDouble(out var posYVal) ? posYVal : null,
            HostedBatches = l.BatchStocks
                .Where(bs => bs.Batch != null)
                .Select(bs => {
                    var taxonomy = bs.Batch!.Taxonomy;
                    string? speciesName = taxonomy?.ScientificName ?? "Unknown Species";
                    
                    if (taxonomy?.CommonNames != null)
                    {
                        if (taxonomy.CommonNames.RootElement.TryGetProperty("en", out var enProp)) speciesName = enProp.GetString();
                        else if (taxonomy.CommonNames.RootElement.TryGetProperty("vi", out var viProp)) speciesName = viProp.GetString();
                    }

                    return new HostedBatchPreviewDto
                    {
                        Id = bs.Batch!.Id,
                        BatchCode = bs.Batch.BatchCode,
                        SpeciesName = speciesName,
                        ImageUrl = taxonomy?.ImageUrl
                    };
                })
                .ToList()
        });
    }
}
