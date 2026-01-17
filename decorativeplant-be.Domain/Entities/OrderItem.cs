namespace decorativeplant_be.Domain.Entities;

public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid ListingId { get; set; }
    public Guid StockId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; } // Price at purchase time
    public string TitleSnapshot { get; set; } = string.Empty;

    // Navigation properties
    public OrderHeader OrderHeader { get; set; } = null!;
    public Listing Listing { get; set; } = null!;
    public BatchStock BatchStock { get; set; } = null!;
    public ICollection<MyGardenPlant> MyGardenPlants { get; set; } = new List<MyGardenPlant>();
}
