using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.Commands;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class CreatePlantBatchCommandHandler : IRequestHandler<CreatePlantBatchCommand, PlantBatchDto>
{
    private readonly IApplicationDbContext _context;

    public CreatePlantBatchCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PlantBatchDto> Handle(CreatePlantBatchCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate Parent Batch if provided
        if (request.ParentBatchId.HasValue)
        {
            var parent = await _context.PlantBatches
                .AnyAsync(x => x.Id == request.ParentBatchId.Value, cancellationToken);
            if (!parent)
            {
                throw new NotFoundException(nameof(PlantBatch), request.ParentBatchId.Value);
            }
        }

        // 2. Resolve Location
        var locationId = request.LocationId;
        if (!locationId.HasValue && request.BranchId.HasValue && request.TaxonomyId.HasValue)
        {
            // Get Taxonomy and Category to enrich Location
            var taxonomy = await _context.PlantTaxonomies
                .Include(t => t.Category)
                .FirstOrDefaultAsync(t => t.Id == request.TaxonomyId.Value, cancellationToken);

            var locationType = request.NewLocationType ?? taxonomy?.Category?.Name ?? "Cultivation";
            var locationName = request.NewLocationName ?? $"{locationType} Area";
            
            // Find or create location for this branch of this type and name (to avoid duplicates)
            var location = await _context.InventoryLocations
                .FirstOrDefaultAsync(x => x.BranchId == request.BranchId && x.Type == locationType && x.Name == locationName, cancellationToken);

            if (location == null)
            {
                var details = request.NewLocationDetails ?? new Dictionary<string, object>();
                
                if (!details.ContainsKey("description"))
                {
                    details["description"] = $"Automatically created area for {locationType} plants.";
                }
                
                if (!details.ContainsKey("environment_type"))
                {
                    details["environment_type"] = locationType.ToLower();
                }

                // Add care info snippets if available and not already provided
                if (taxonomy?.CareInfo != null && !details.ContainsKey("suggested_care"))
                {
                    details["suggested_care"] = taxonomy.CareInfo;
                }

                location = new InventoryLocation
                {
                    Id = Guid.NewGuid(),
                    BranchId = request.BranchId,
                    Name = locationName,
                    Type = locationType,
                    Code = $"{locationType.Substring(0, Math.Min(4, locationType.Length)).ToUpper()}-{request.BranchId.Value.ToString().Substring(0, 4).ToUpper()}",
                    Details = JsonDocument.Parse(JsonSerializer.Serialize(details))
                };
                _context.InventoryLocations.Add(location);
            }
            locationId = location.Id;
        }
        else if (!locationId.HasValue && request.BranchId.HasValue)
        {
            // Legacy/Fallback: Find or create 'Cultivation' location
            var location = await _context.InventoryLocations
                .FirstOrDefaultAsync(x => x.BranchId == request.BranchId && x.Type == "Cultivation", cancellationToken);

            if (location == null)
            {
                location = new InventoryLocation
                {
                    Id = Guid.NewGuid(),
                    BranchId = request.BranchId,
                    Name = "Default Cultivation Area",
                    Type = "Cultivation",
                    Code = $"CULT-{request.BranchId.Value.ToString().Substring(0, 4).ToUpper()}",
                    Details = JsonDocument.Parse(JsonSerializer.Serialize(new { description = "Automatically created cultivation area" }))
                };
                _context.InventoryLocations.Add(location);
            }
            locationId = location.Id;
        }

        // 3. Generate Batch Code
        // BATCH-{yyyyMMdd}-{4_char_random}
        var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
        var randomPart = Guid.NewGuid().ToString().Substring(0, 4).ToUpper();
        var batchCode = string.IsNullOrEmpty(request.BatchCode) 
            ? $"BATCH-{datePart}-{randomPart}" 
            : request.BatchCode;

        // 4. Create PlantBatch
        var entity = new PlantBatch
        {
            Id = Guid.NewGuid(),
            BatchCode = batchCode,
            BranchId = request.BranchId,
            TaxonomyId = request.TaxonomyId,
            SupplierId = request.SupplierId,
            ParentBatchId = request.ParentBatchId,
            SourceInfo = PlantBatchMapper.BuildJson(request.SourceInfo),
            Specs = PlantBatchMapper.BuildJson(request.Specs),
            InitialQuantity = request.InitialQuantity,
            CurrentTotalQuantity = request.InitialQuantity,
            CreatedAt = DateTime.UtcNow
        };

        _context.PlantBatches.Add(entity);

        // 5. Create BatchStock if location and quantity exist
        if (locationId.HasValue && request.InitialQuantity > 0)
        {
            var stock = new BatchStock
            {
                Id = Guid.NewGuid(),
                BatchId = entity.Id,
                LocationId = locationId.Value,
                Quantities = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    quantity = request.InitialQuantity,
                    available_quantity = 0,
                    reserved_quantity = request.InitialQuantity,
                    total_received = 0
                })),
                HealthStatus = ExtractHealthStatus(request.Specs) ?? "Healthy",
                UpdatedAt = DateTime.UtcNow
            };
            _context.BatchStocks.Add(stock);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Fetch relationships for full DTO
        var result = await _context.PlantBatches
            .Include(x => x.Taxonomy)
            .Include(x => x.Supplier)
            .Include(x => x.Branch)
            .Include(x => x.ParentBatch)
            .FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);

        return PlantBatchMapper.ToDto(result!);
    }

    private string? ExtractHealthStatus(Dictionary<string, object>? specs)
    {
        if (specs != null && specs.TryGetValue("health_status", out var status) && status != null)
        {
            return status.ToString();
        }
        return null;
    }
}
