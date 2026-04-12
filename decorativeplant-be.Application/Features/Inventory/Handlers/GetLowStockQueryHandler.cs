using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Queries;
using decorativeplant_be.Domain.Entities;
using MediatR;
using System.Text.Json;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class GetLowStockQueryHandler : IRequestHandler<GetLowStockQuery, List<LowStockItemDto>>
{
    private readonly IApplicationDbContext _context;

    public GetLowStockQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<LowStockItemDto>> Handle(GetLowStockQuery request, CancellationToken cancellationToken)
    {
        // Query ONLY batches that have been published to stock (have BatchStock records)
        var query = _context.PlantBatches
            .Include(b => b.Branch)
            .Include(b => b.Taxonomy)
            .Include(b => b.ProductListings)
            .Include(b => b.BatchStocks)
            .Where(b => b.BatchStocks.Any()); // Essential: Only shows items sent to sales

        if (request.BranchId.HasValue)
        {
            query = query.Where(b => b.BranchId == request.BranchId);
        }

        var batches = await query.ToListAsync(cancellationToken);

        var dtos = new List<LowStockItemDto>();

        foreach (var batch in batches)
        {
            // Calculate total stock available for sale across all locations for this batch at this branch
            int salesStock = 0;
            if (batch.BatchStocks != null)
            {
                foreach (var s in batch.BatchStocks)
                {
                    if (s.Quantities != null && s.Quantities.RootElement.TryGetProperty("available_quantity", out var aq))
                    {
                        salesStock += aq.GetInt32();
                    }
                }
            }

            // Find associated product for price and category info
            var product = batch.ProductListings.FirstOrDefault();
            string productId = batch.Id.ToString(); 
            string price = "0";
            string category = "Uncategorized";

            if (product != null && product.ProductInfo != null)
            {
                productId = product.Id.ToString();
                var root = product.ProductInfo.RootElement;
                if (root.TryGetProperty("price", out var priceProp))
                    price = GetJsonString(priceProp) ?? "0";

                if (product.StatusInfo != null && product.StatusInfo.RootElement.TryGetProperty("tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array && tagsProp.GetArrayLength() > 0)
                    category = GetJsonString(tagsProp[0]) ?? category;
            }

            // Consistent naming logic: prioritize English common name from Taxonomy, fallback to Scientific Name
            string speciesDisplayName = "Unknown Product";
            if (batch.Taxonomy != null)
            {
                string? enName = null;
                if (batch.Taxonomy.CommonNames != null && batch.Taxonomy.CommonNames.RootElement.TryGetProperty("en", out var enProp))
                {
                    enName = enProp.GetString();
                }
                
                speciesDisplayName = !string.IsNullOrEmpty(enName) 
                    ? enName 
                    : (batch.Taxonomy.ScientificName ?? "Unknown Product");
            }
            
            // Format name specifically for inventory tracking
            string productName = $"{speciesDisplayName} (Batch {batch.BatchCode ?? batch.Id.ToString().Substring(0, 8)})";

            dtos.Add(new LowStockItemDto
            {
                ProductId = productId,
                ProductName = productName,
                Price = price,
                Category = category,
                CurrentStock = salesStock,
                Threshold = 10,
                BranchName = batch.Branch?.Name ?? "Global",
                BatchId = batch.Id,
                BatchCode = batch.BatchCode ?? "N/A",
                TaxonomyId = batch.TaxonomyId
            });
        }

        return dtos;
    }

    private static string? GetJsonString(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => null,
        _ => element.GetRawText()
    };
}
