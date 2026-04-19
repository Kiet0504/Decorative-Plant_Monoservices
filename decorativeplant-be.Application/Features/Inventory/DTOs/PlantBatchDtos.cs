using System.Text.Json.Serialization;

namespace decorativeplant_be.Application.Features.Inventory.DTOs;

public class PlantBatchDto
{
    public Guid Id { get; set; }
    public string? BatchCode { get; set; }
    public Guid? ParentBatchId { get; set; }
    public string? ParentBatchCode { get; set; } // For display
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; } // Display
    public Guid? TaxonomyId { get; set; }
    public string? SpeciesName { get; set; } // Display
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; } // Display
    
    // JSONB Fields
    public object? SourceInfo { get; set; }
    public object? Specs { get; set; }
    // Display Fields
    public string? HealthStatus { get; set; }
    public string? Stage { get; set; }
    
    [JsonPropertyName("initialQuantity")]
    public int InitialQuantity { get; set; }

    [JsonPropertyName("currentTotalQuantity")]
    public int CurrentTotalQuantity { get; set; }

    // Aggregate stock fields
    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
    
    [JsonPropertyName("reserved_quantity")]
    public int ReservedQuantity { get; set; }
    
    [JsonPropertyName("available_quantity")]
    public int AvailableQuantity { get; set; }
    
    [JsonPropertyName("total_received")]
    public int TotalReceived { get; set; }

    public decimal? PurchaseCost { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class PlantBatchSummaryDto
{
    public Guid Id { get; set; }
    public string? BatchCode { get; set; }
    public string? SpeciesName { get; set; }
    public string? BranchName { get; set; } // Added
    public string? HealthStatus { get; set; }
    public string? Stage { get; set; }
    
    [JsonPropertyName("initialQuantity")]
    public int InitialQuantity { get; set; }

    [JsonPropertyName("currentTotalQuantity")]
    public int CurrentTotalQuantity { get; set; }

    // Aggregate stock fields
    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
    
    [JsonPropertyName("reserved_quantity")]
    public int ReservedQuantity { get; set; }
    
    [JsonPropertyName("available_quantity")]
    public int AvailableQuantity { get; set; }
    
    [JsonPropertyName("total_received")]
    public int TotalReceived { get; set; }

    public DateTime? CreatedAt { get; set; }
}

public class CreatePlantBatchDto
{
    public Guid? BranchId { get; set; }
    public Guid? TaxonomyId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? ParentBatchId { get; set; } // Optional: for propagation
    
    public Dictionary<string, object>? SourceInfo { get; set; }
    public Dictionary<string, object>? Specs { get; set; }
    
    public int InitialQuantity { get; set; }
    public decimal? PurchaseCost { get; set; }
}

public class UpdatePlantBatchDto
{
    public Guid Id { get; set; }
    public Guid? BranchId { get; set; } // Added
    public int? InitialQuantity { get; set; }
    public int? CurrentTotalQuantity { get; set; }
    public Dictionary<string, object>? SourceInfo { get; set; }
    public Dictionary<string, object>? Specs { get; set; }
    public decimal? PurchaseCost { get; set; }
}
