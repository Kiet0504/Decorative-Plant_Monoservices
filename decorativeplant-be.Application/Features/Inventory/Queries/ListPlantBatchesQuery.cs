using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Queries;

public class ListPlantBatchesQuery : IRequest<PagedResultDto<PlantBatchSummaryDto>>
{
    public string? SearchTerm { get; set; } // Batch Code or Species Name
    public Guid? TaxonomyId { get; set; }
    public Guid? SupplierId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
