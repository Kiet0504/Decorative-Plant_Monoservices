using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.PlantLibrary.DTOs;
using decorativeplant_be.Application.Features.PlantLibrary.Queries;
using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Domain.Entities;
using MediatR;
using System.Linq.Expressions;
using System.Text.Json;

namespace decorativeplant_be.Application.Features.PlantLibrary.Handlers;

public class ListSuppliersQueryHandler : IRequestHandler<ListSuppliersQuery, PagedResultDto<SupplierDto>>
{
    private readonly IRepositoryFactory _repositoryFactory;

    public ListSuppliersQueryHandler(IRepositoryFactory repositoryFactory)
    {
        _repositoryFactory = repositoryFactory;
    }

    public async Task<PagedResultDto<SupplierDto>> Handle(ListSuppliersQuery request, CancellationToken cancellationToken)
    {
        var repo = _repositoryFactory.CreateRepository<Supplier>();

        Expression<Func<Supplier, bool>> filter = s => string.IsNullOrEmpty(request.SearchTerm) || (s.Name != null && s.Name.Contains(request.SearchTerm));

        var totalCount = await repo.CountAsync(filter, cancellationToken);
        
        // Manual pagination since IRepository doesn't expose IQueryable directly or a Paged method
        // We might need to use FindAsync and then client-side paging if repo doesn't support skip/take?
        // Wait, generic repository returns IEnumerable on FindAsync, which fetches all.
        // If IRepository doesn't support Skip/Take, we have to fetch all matching and page in memory (bad for performance but safest for now without changing infra)
        // OR check if IRepository has GetQueryable? No it doesn't.
        
        // NOTE: The current IRepository definition is limited. Efficiency is compromised here.
        // Ideally we should extend IRepository to support IQueryable or Skip/Take.
        // For now, fetch all matching and page in memory.
        
        var allItems = await repo.FindAsync(filter, cancellationToken);
        var pagedItems = allItems.OrderBy(s => s.Name)
                                 .Skip((request.Page - 1) * request.PageSize)
                                 .Take(request.PageSize)
                                 .ToList();

        var dtos = pagedItems.Select(SupplierMapper.ToDto).ToList();

        return new PagedResultDto<SupplierDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
