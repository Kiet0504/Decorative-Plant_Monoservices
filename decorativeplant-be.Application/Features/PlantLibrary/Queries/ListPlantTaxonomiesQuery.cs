using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Features.PlantLibrary.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.PlantLibrary.Queries;

public class ListPlantTaxonomiesQuery : IRequest<PagedResultDto<PlantTaxonomySummaryDto>>
{
    public string? SearchTerm { get; set; }
    public Guid? CategoryId { get; set; }
    public bool OnlyWithActiveListings { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
