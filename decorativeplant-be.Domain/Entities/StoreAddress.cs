using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries;

namespace decorativeplant_be.Domain.Entities;

public class StoreAddress
{
    // Note: StoreAddress in ERD is presented as 'store_address' but seems to be used as a value type or separate table.
    // Given the ERD has 'store_address' table with id, I will make it an entity.
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public Store Store { get; set; } = null!;
    
    public string FullAddressText { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public Point? Coordinates { get; set; } = null!;
    public bool IsDefaultPickup { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
