using decorativeplant_be.Application.Common.DTOs.Garden;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Commands;

public enum PurchaseImportCreateMode
{
    OnePerItem = 0,
    OnePerQuantity = 1
}

public class ImportGardenPlantsFromPurchaseCommand : IRequest<IReadOnlyList<GardenPlantDto>>
{
    /// <summary>Set by controller from JWT. Required.</summary>
    public Guid UserId { get; set; }

    public List<Guid> OrderItemIds { get; set; } = new();

    public PurchaseImportCreateMode CreateMode { get; set; } = PurchaseImportCreateMode.OnePerItem;

    public string? Nickname { get; set; }

    public string? Location { get; set; }

    /// <summary>ISO date string; if null we may infer from order CreatedAt.</summary>
    public string? AdoptedDate { get; set; }

    public string? ImageUrl { get; set; }

    /// <summary>healthy|needs_attention|struggling</summary>
    public string? Health { get; set; }

    /// <summary>small|medium|large</summary>
    public string? Size { get; set; }
}

