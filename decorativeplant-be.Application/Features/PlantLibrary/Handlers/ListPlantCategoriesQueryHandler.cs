using MediatR;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.PlantLibrary.DTOs;
using decorativeplant_be.Application.Features.PlantLibrary.Queries;

namespace decorativeplant_be.Application.Features.PlantLibrary.Handlers;

public class ListPlantCategoriesQueryHandler : IRequestHandler<ListPlantCategoriesQuery, PagedResult<PlantCategoryDto>>
{
    private readonly IApplicationDbContext _context;

    public ListPlantCategoriesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<PlantCategoryDto>> Handle(ListPlantCategoriesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.PlantCategories.AsQueryable();

        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            query = query.Where(x => (x.Name != null && x.Name.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .OrderBy(x => x.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new PlantCategoryDto
            {
                Id = x.Id,
                Name = x.Name ?? "Unnamed",
                Slug = x.Slug,
                ParentId = x.ParentId,
                IconUrl = x.IconUrl
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<PlantCategoryDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
