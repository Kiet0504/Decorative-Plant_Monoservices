using decorativeplant_be.Application.Common.DTOs.ArPreview;
using decorativeplant_be.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace decorativeplant_be.Application.Features.ArPreview.Handlers;

public class GetAllProductModelAssetsQueryHandler
    : IRequestHandler<Queries.GetAllProductModelAssetsQuery, List<ProductModelAssetListItemResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetAllProductModelAssetsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProductModelAssetListItemResponse>> Handle(
        Queries.GetAllProductModelAssetsQuery request,
        CancellationToken cancellationToken)
    {
        var assets = await _context.ProductModelAssets
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return assets.Select(asset => new ProductModelAssetListItemResponse
        {
            Id = asset.Id,
            CreatedAt = asset.CreatedAt,
            ProductListingId = asset.ProductListingId,
            GlbUrl = asset.GlbUrl,
            DefaultScale = asset.DefaultScale,
            BoundingBox = asset.BoundingBox == null
                ? null
                : JsonDocument.Parse(asset.BoundingBox.RootElement.GetRawText())
        }).ToList();
    }
}

