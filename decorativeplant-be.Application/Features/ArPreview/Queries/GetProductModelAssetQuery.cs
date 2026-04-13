using decorativeplant_be.Application.Common.DTOs.ArPreview;
using MediatR;

namespace decorativeplant_be.Application.Features.ArPreview.Queries;

public class GetProductModelAssetQuery : IRequest<ProductModelAssetResponse?>
{
    public Guid ProductListingId { get; set; }
}

