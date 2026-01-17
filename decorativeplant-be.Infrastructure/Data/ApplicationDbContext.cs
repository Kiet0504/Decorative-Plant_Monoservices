using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Identity & Users
    public DbSet<UserAccount> UserAccounts { get; set; } = null!;
    public DbSet<UserProfile> UserProfiles { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;

    // Store & Inventory
    public DbSet<Store> Stores { get; set; } = null!;
    public DbSet<StoreAddress> StoreAddresses { get; set; } = null!;
    public DbSet<StoreWallet> StoreWallets { get; set; } = null!;
    public DbSet<WalletTransaction> WalletTransactions { get; set; } = null!;
    public DbSet<PlatformFeePolicy> PlatformFeePolicies { get; set; } = null!;
    public DbSet<SellerPackage> SellerPackages { get; set; } = null!;
    public DbSet<SellerSubscription> SellerSubscriptions { get; set; } = null!;
    public DbSet<InventoryLocation> InventoryLocations { get; set; } = null!;
    public DbSet<PlantTaxonomy> PlantTaxonomies { get; set; } = null!;
    public DbSet<PlantBatch> PlantBatches { get; set; } = null!;
    public DbSet<BatchStock> BatchStocks { get; set; } = null!;
    public DbSet<BatchLog> BatchLogs { get; set; } = null!;
    public DbSet<InventoryAdjustment> InventoryAdjustments { get; set; } = null!;
    public DbSet<StorePlantDiagnosis> StorePlantDiagnoses { get; set; } = null!;

    // IoT & Automation
    public DbSet<IotDevice> IotDevices { get; set; } = null!;
    public DbSet<SensorReading> SensorReadings { get; set; } = null!;
    public DbSet<AutoRule> AutoRules { get; set; } = null!;
    public DbSet<RuleExecutionLog> RuleExecutionLogs { get; set; } = null!;

    // Commerce
    public DbSet<Listing> Listings { get; set; } = null!;
    public DbSet<Voucher> Vouchers { get; set; } = null!;
    public DbSet<ShoppingCart> ShoppingCarts { get; set; } = null!;
    public DbSet<ProductReview> ProductReviews { get; set; } = null!;

    // Orders & Shipping
    public DbSet<OrderHeader> OrderHeaders { get; set; } = null!;
    public DbSet<OrderItem> OrderItems { get; set; } = null!;
    public DbSet<PaymentTransaction> PaymentTransactions { get; set; } = null!;
    public DbSet<Shipping> Shippings { get; set; } = null!;
    public DbSet<PickupAddressSnapshot> PickupAddressSnapshots { get; set; } = null!;
    public DbSet<ShippingAddressSnapshot> ShippingAddressSnapshots { get; set; } = null!;

    // My Garden
    public DbSet<MyGardenPlant> MyGardenPlants { get; set; } = null!;
    public DbSet<CareSchedule> CareSchedules { get; set; } = null!;
    public DbSet<CareLog> CareLogs { get; set; } = null!;
    public DbSet<DiagnosisLog> DiagnosisLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply configurations from Configurations folder
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
