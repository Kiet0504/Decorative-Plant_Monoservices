using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Features.PlantLibrary.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.PlantLibrary.Queries;

public class ListSuppliersQuery : IRequest<PagedResultDto<SupplierDto>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
}
