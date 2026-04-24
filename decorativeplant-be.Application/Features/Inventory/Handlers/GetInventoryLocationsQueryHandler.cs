using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Queries;
using decorativeplant_be.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

        try
        {
            var locations = await query.ToListAsync(cancellationToken);

            return locations.Select(l => {
                try 
                {
                    var details = l.Details?.RootElement;
                    var isObject = details?.ValueKind == JsonValueKind.Object;

                    return new InventoryLocationDto
                    {
                        Id = l.Id,
                        BranchId = l.BranchId,
                        ParentLocationId = l.ParentLocationId,
                        Code = l.Code,
                        Name = l.Name,
                        Type = l.Type,
                        Description = (isObject && details!.Value.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.String) ? desc.GetString() : null,
                        Capacity = (isObject && details!.Value.TryGetProperty("capacity", out var cap) && cap.ValueKind == JsonValueKind.Number && cap.TryGetInt32(out var capVal)) ? capVal : null,
                        CurrentOccupancy = l.BatchStocks
                            .Where(bs => bs.Batch != null && bs.Batch.BranchId == l.BranchId)
                            .Sum(bs => {
                                if (bs.Quantities == null) return 0;
                                var root = bs.Quantities.RootElement;
                                
                                if (root.TryGetProperty("reserved_quantity", out var rqProp) && rqProp.ValueKind == JsonValueKind.Number && rqProp.TryGetDouble(out var rqVal))
                                    return (int)rqVal;
                                
                                if (root.TryGetProperty("quantity", out var qProp) && qProp.ValueKind == JsonValueKind.Number && qProp.TryGetDouble(out var qVal))
                                    return (int)qVal;
                                    
                                return 0;
                            }),
                        EnvironmentType = (isObject && details!.Value.TryGetProperty("environment_type", out var env) && env.ValueKind == JsonValueKind.String) ? env.GetString() : null,
                        PositionX = (isObject && details!.Value.TryGetProperty("position_x", out var posX) && posX.ValueKind == JsonValueKind.Number && posX.TryGetDouble(out var posXVal)) ? posXVal : null,
                        PositionY = (isObject && details!.Value.TryGetProperty("position_y", out var posY) && posY.ValueKind == JsonValueKind.Number && posY.TryGetDouble(out var posYVal)) ? posYVal : null,
                        HostedBatches = l.BatchStocks
                            .Where(bs => bs.Batch != null && bs.Batch.BranchId == l.BranchId)
                            .Where(bs => {
                                if (bs.Quantities == null) return false;
                                return bs.Quantities.RootElement.TryGetProperty("reserved_quantity", out var rq) && 
                                       rq.ValueKind == JsonValueKind.Number && rq.TryGetDouble(out var val) && val > 0;
                            })
                            .GroupBy(bs => bs.BatchId)
                            .Select(g => g.First())
                            .Select(bs => {
                                var taxonomy = bs.Batch!.Taxonomy;
                                string? speciesName = taxonomy?.ScientificName ?? "Unknown Species";
                                
                                if (taxonomy?.CommonNames != null && taxonomy.CommonNames.RootElement.ValueKind == JsonValueKind.Object)
                                {
                                    if (taxonomy.CommonNames.RootElement.TryGetProperty("en", out var enProp) && enProp.ValueKind == JsonValueKind.String) speciesName = enProp.GetString();
                                    else if (taxonomy.CommonNames.RootElement.TryGetProperty("vi", out var viProp) && viProp.ValueKind == JsonValueKind.String) speciesName = viProp.GetString();
                                }

                                int qty = 0;
                                if (bs.Quantities != null && bs.Quantities.RootElement.TryGetProperty("reserved_quantity", out var rq) && rq.ValueKind == JsonValueKind.Number && rq.TryGetDouble(out var val))
                                {
                                    qty = (int)val;
                                }

                                return new HostedBatchPreviewDto
                                {
                                    Id = bs.Batch!.Id,
                                    BatchCode = bs.Batch.BatchCode,
                                    SpeciesName = speciesName,
                                    ImageUrl = taxonomy?.ImageUrl,
                                    Quantity = qty
                                };
                            })
                            .ToList()
                    };
                }
                catch (Exception ex)
                {
                    return new InventoryLocationDto
                    {
                        Id = l.Id,
                        Name = "ERROR_IN_PROJECTION",
                        Description = $"Location ID: {l.Id}. Error: {ex.Message}. Trace: {ex.StackTrace?.Substring(0, Math.Min(ex.StackTrace.Length, 200))}",
                        Type = "Error"
                    };
                }
            }).ToList();
        }
        catch (Exception ex)
        {
             return new List<InventoryLocationDto> { 
                 new InventoryLocationDto { 
                     Name = "DATABASE_QUERY_ERROR", 
                     Description = $"Critical error: {ex.Message}. Inner: {ex.InnerException?.Message}",
                     Type = "Error"
                 } 
             };
        }
    }
}
