using decorativeplant_be.Application.Common.DTOs.Diagnosis;
using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Diagnosis.Queries;
using MediatR;

namespace decorativeplant_be.Application.Features.Diagnosis.Handlers;

public class GetDiagnosesQueryHandler : IRequestHandler<GetDiagnosesQuery, PagedResultDto<PlantDiagnosisDto>>
{
    private readonly IGardenRepository _gardenRepository;

    public GetDiagnosesQueryHandler(IGardenRepository gardenRepository)
    {
        _gardenRepository = gardenRepository;
    }

    public async Task<PagedResultDto<PlantDiagnosisDto>> Handle(GetDiagnosesQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _gardenRepository.GetDiagnosesByUserIdAsync(
            request.UserId,
            request.GardenPlantId,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResultDto<PlantDiagnosisDto>
        {
            Items = items.Select(DiagnosisMapper.ToDto).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
