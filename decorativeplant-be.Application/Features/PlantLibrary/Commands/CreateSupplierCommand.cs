using decorativeplant_be.Application.Features.PlantLibrary.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.PlantLibrary.Commands;

public class CreateSupplierCommand : IRequest<SupplierDto>
{
    public string Name { get; set; } = string.Empty;
    public string? ContactInfo { get; set; }
    public string? Address { get; set; }
}
