using decorativeplant_be.Application.Common.DTOs.Garden;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Queries;

/// <summary>
/// Resolves plant taxonomy from an order item (batch/listing chain), for add-from-purchase UI preview.
/// </summary>
public sealed class GetOrderItemTaxonomyPreviewQuery : IRequest<OrderItemTaxonomyPreviewDto>
{
    public Guid UserId { get; set; }

    public Guid OrderItemId { get; set; }
}
