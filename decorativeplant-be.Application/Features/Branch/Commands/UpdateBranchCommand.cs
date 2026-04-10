// decorativeplant-be.Application/Features/Branch/Commands/UpdateBranchCommand.cs

using System.Text.Json;
using decorativeplant_be.Application.Features.Branch.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Branch.Commands;

public record UpdateBranchCommand : IRequest<BranchDto>
{
    public Guid Id { get; init; }
    // Code cannot be changed after creation
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? BranchType { get; init; }
    public string? ContactPhone { get; init; }
    public string? ContactEmail { get; init; }
    public string? FullAddress { get; init; }
    public string? City { get; init; }
    public double? Lat { get; init; }
    public double? Long { get; init; }
    public JsonDocument? OperatingHours { get; init; }
    public bool SupportsOnlineOrder { get; init; }
    public bool SupportsPickup { get; init; }
    public bool SupportsShipping { get; init; }
}
