using MediatR;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Features.PlantLibrary.DTOs;

namespace decorativeplant_be.Application.Features.PlantLibrary.Queries;

public class ListPlantCategoriesQuery : IRequest<PagedResult<PlantCategoryDto>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 100; // Default to a large number since categories are few
    public string? SearchTerm { get; set; }
}
