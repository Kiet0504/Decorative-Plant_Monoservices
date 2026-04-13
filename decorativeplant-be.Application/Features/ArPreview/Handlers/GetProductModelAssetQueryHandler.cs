using decorativeplant_be.Application.Common.DTOs.ArPreview;
using decorativeplant_be.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace decorativeplant_be.Application.Features.ArPreview.Handlers;

public class GetProductModelAssetQueryHandler : IRequestHandler<Queries.GetProductModelAssetQuery, ProductModelAssetResponse?>
{
    private readonly IApplicationDbContext _context;

    public GetProductModelAssetQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProductModelAssetResponse?> Handle(Queries.GetProductModelAssetQuery query, CancellationToken cancellationToken)
    {
        var asset = await _context.ProductModelAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProductListingId == query.ProductListingId, cancellationToken);

        if (asset == null) return null;

        return new ProductModelAssetResponse
        {
            ProductListingId = asset.ProductListingId,
            GlbUrl = asset.GlbUrl,
            DefaultScale = asset.DefaultScale,
            BoundingBox = asset.BoundingBox == null ? null : JsonDocument.Parse(asset.BoundingBox.RootElement.GetRawText())
        };
    }
}

