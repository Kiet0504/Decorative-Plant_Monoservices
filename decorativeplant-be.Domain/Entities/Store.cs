namespace decorativeplant_be.Domain.Entities;

public class Store : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string Status { get; set; } = string.Empty; // Active/Suspended
    public Guid? CurrentSubscriptionId { get; set; }

    // Navigation properties
    public UserAccount OwnerUser { get; set; } = null!;
    public SellerSubscription? CurrentSubscription { get; set; }
    public ICollection<StoreAddress> StoreAddresses { get; set; } = new List<StoreAddress>();
    public ICollection<StoreWallet> StoreWallets { get; set; } = new List<StoreWallet>();
    public ICollection<SellerSubscription> Subscriptions { get; set; } = new List<SellerSubscription>();
    public ICollection<InventoryLocation> InventoryLocations { get; set; } = new List<InventoryLocation>();
    public ICollection<PlantBatch> PlantBatches { get; set; } = new List<PlantBatch>();
    public ICollection<IotDevice> IotDevices { get; set; } = new List<IotDevice>();
    public ICollection<Listing> Listings { get; set; } = new List<Listing>();
    public ICollection<Voucher> Vouchers { get; set; } = new List<Voucher>();
    public ICollection<OrderHeader> Orders { get; set; } = new List<OrderHeader>();
}
