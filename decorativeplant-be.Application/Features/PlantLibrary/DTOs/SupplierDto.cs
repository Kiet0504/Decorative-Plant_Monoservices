namespace decorativeplant_be.Application.Features.PlantLibrary.DTOs;

public class SupplierDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactInfo { get; set; }
    public string? Address { get; set; }
}
