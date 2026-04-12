using decorativeplant_be.Application.Features.Cultivation.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Cultivation.Commands;

public class CreateBatchCareTaskCommand : IRequest<Guid>
{
    public string ProductName { get; set; } = string.Empty;
    public string Activity { get; set; } = string.Empty; // Mapping to ActivityType
    public string Batch { get; set; } = string.Empty;    // BatchCode
    public string Frequency { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;     // Mapping to DueDate
    public string RepeatEvery { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CareRequirement { get; set; } = string.Empty;
}
