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
        // Query batches that match threshold and branch
        var batches = await _context.PlantBatches
            .Include(b => b.Branch)
            .Include(b => b.Taxonomy)
            .Include(b => b.ProductListings)
            .Where(b => (b.CurrentTotalQuantity ?? 0) < request.Threshold &&
                       (!request.BranchId.HasValue || b.BranchId == request.BranchId))
            .ToListAsync(cancellationToken);

        var dtos = new List<LowStockItemDto>();

        foreach (var batch in batches)
        {
            // Find the best associated product for display
            var product = batch.ProductListings.FirstOrDefault();
            string productName = "Unknown Product";
            string productId = batch.Id.ToString(); 
            string price = "0";
            string category = "Uncategorized";

            if (product != null && product.ProductInfo != null)
            {
                productId = product.Id.ToString();
                var root = product.ProductInfo.RootElement;
                if (root.TryGetProperty("title", out var titleProp))
                    productName = GetJsonString(titleProp) ?? productName;
                
                if (root.TryGetProperty("price", out var priceProp))
                    price = GetJsonString(priceProp) ?? "0";

                if (product.StatusInfo != null && product.StatusInfo.RootElement.TryGetProperty("tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array && tagsProp.GetArrayLength() > 0)
                    category = GetJsonString(tagsProp[0]) ?? category;
            }
            else if (batch.Taxonomy != null)
            {
                productName = batch.Taxonomy.ScientificName ?? productName;
            }

            dtos.Add(new LowStockItemDto
            {
                ProductId = productId,
                ProductName = productName,
                Price = price,
                Category = category,
                CurrentStock = batch.CurrentTotalQuantity ?? 0,
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
