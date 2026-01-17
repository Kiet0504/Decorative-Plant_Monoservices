using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

public class IotDevice : BaseEntity
{
    public Guid StoreId { get; set; }
    public Guid? LocationId { get; set; } // Attached to Location (bulk) OR Stock (expensive Bonsai)
    public Guid? StockId { get; set; } // Null if attached to Location
    public string Name { get; set; } = string.Empty; // e.g., "Soil sensor Tray 05"
    public string? MacAddress { get; set; }
    public string? FirmwareVer { get; set; }
    public string Status { get; set; } = string.Empty; // Online/Offline
    public JsonDocument? ComponentsJson { get; set; } // List of sensors: {temp: true, soil_moisture: true}
    public DateTime? LastSeenAt { get; set; }

    // Navigation properties
    public Store Store { get; set; } = null!;
    public InventoryLocation? Location { get; set; }
    public BatchStock? Stock { get; set; }
    public ICollection<SensorReading> SensorReadings { get; set; } = new List<SensorReading>();
    public ICollection<AutoRule> AutoRules { get; set; } = new List<AutoRule>();
}
