using decorativeplant_be.Application.Common.DTOs.ArPreview;
using MediatR;

namespace decorativeplant_be.Application.Features.ArPreview.Queries;

public class GetAllProductModelAssetsQuery : IRequest<List<ProductModelAssetListItemResponse>>
{
}

