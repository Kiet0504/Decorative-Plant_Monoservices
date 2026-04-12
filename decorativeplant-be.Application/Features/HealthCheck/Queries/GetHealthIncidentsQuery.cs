using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Features.HealthCheck.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.HealthCheck.Queries;

public class GetHealthIncidentsQuery : IRequest<PagedResult<HealthIncidentDto>>
{
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
    public string? Severity { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
