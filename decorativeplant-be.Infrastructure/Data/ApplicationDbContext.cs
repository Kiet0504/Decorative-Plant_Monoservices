using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Domain.Entities;
using decorativeplant_be.Application.Common.Interfaces;
using System.Linq.Expressions;

namespace decorativeplant_be.Infrastructure.Data;

/// <summary>
/// DbContext for Smart Ornamental Plant Support System. Schema per DBML/PostgresDB.dbml.
/// </summary>
public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Module 1: Identity & Access
    public DbSet<UserAccount> UserAccounts { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<UserSubscription> UserSubscriptions { get; set; } = null!;
    public DbSet<FeatureUsageQuota> FeatureUsageQuotas { get; set; } = null!;
    public DbSet<PremiumFeature> PremiumFeatures { get; set; } = null!;

    // Module 2: Company & Branch
    public DbSet<Company> Companies { get; set; } = null!;
    public DbSet<Branch> Branches { get; set; } = null!;
    public DbSet<StaffAssignment> StaffAssignments { get; set; } = null!;

    // Module 3: Inventory & Cultivation
    public DbSet<PlantCategory> PlantCategories { get; set; } = null!;
    public DbSet<PlantTaxonomy> PlantTaxonomies { get; set; } = null!;
    public DbSet<InventoryLocation> InventoryLocations { get; set; } = null!;
    public DbSet<Supplier> Suppliers { get; set; } = null!;
    public DbSet<PlantBatch> PlantBatches { get; set; } = null!;
    public DbSet<BatchStock> BatchStocks { get; set; } = null!;
    public DbSet<StockAdjustment> StockAdjustments { get; set; } = null!;
    public DbSet<StockTransfer> StockTransfers { get; set; } = null!;
    public DbSet<CultivationLog> CultivationLogs { get; set; } = null!;
    public DbSet<HealthIncident> HealthIncidents { get; set; } = null!;

    // Module 4: IoT & Automation
    public DbSet<IotDevice> IotDevices { get; set; } = null!;
    public DbSet<SensorReading> SensorReadings { get; set; } = null!;
    public DbSet<AutomationRule> AutomationRules { get; set; } = null!;
    public DbSet<AutomationExecutionLog> AutomationExecutionLogs { get; set; } = null!;
    public DbSet<IotAlert> IotAlerts { get; set; } = null!;

    // Module 5: Commerce
    public DbSet<ProductListing> ProductListings { get; set; } = null!;
    public DbSet<ShippingZone> ShippingZones { get; set; } = null!;
    public DbSet<Voucher> Vouchers { get; set; } = null!;
    public DbSet<Promotion> Promotions { get; set; } = null!;
    public DbSet<ShoppingCart> ShoppingCarts { get; set; } = null!;
    public DbSet<WishlistItem> WishlistItems { get; set; } = null!;
    public DbSet<OrderHeader> OrderHeaders { get; set; } = null!;
    public DbSet<OrderItem> OrderItems { get; set; } = null!;
    public DbSet<PaymentTransaction> PaymentTransactions { get; set; } = null!;
    public DbSet<Shipping> Shippings { get; set; } = null!;
    public DbSet<ReturnRequest> ReturnRequests { get; set; } = null!;

    // Module 6: My Garden
    public DbSet<GardenPlant> GardenPlants { get; set; } = null!;
    public DbSet<CareSchedule> CareSchedules { get; set; } = null!;
    public DbSet<CareLog> CareLogs { get; set; } = null!;
    public DbSet<PlantDiagnosis> PlantDiagnoses { get; set; } = null!;

    // Module 7: Reviews
    public DbSet<ProductReview> ProductReviews { get; set; } = null!;

    // Module 8: Analytics & Config
    public DbSet<AiTrainingFeedback> AiTrainingFeedbacks { get; set; } = null!;
    public DbSet<SystemConfig> SystemConfigs { get; set; } = null!;
    public DbSet<RecommendationLog> RecommendationLogs { get; set; } = null!;

    // Module 9: AR Preview
    public DbSet<ArPreviewSession> ArPreviewSessions { get; set; } = null!;
    public DbSet<ProductModelAsset> ProductModelAssets { get; set; } = null!;

    // Module 10: AI Assistant chat history
    public DbSet<AiChatThread> AiChatThreads { get; set; } = null!;
    public DbSet<AiChatMessage> AiChatMessages { get; set; } = null!;

    // ── Pessimistic Locking ──

    public async Task AcquireStockLockAsync(Guid listingId, CancellationToken ct = default)
    {
        await Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM \"product_listing\" WHERE \"Id\" = {0} FOR UPDATE",
            new object[] { listingId }, ct);
    }

    public async Task AcquireVoucherLockAsync(Guid voucherId, CancellationToken ct = default)
    {
        await Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM \"voucher\" WHERE \"Id\" = {0} FOR UPDATE",
            new object[] { voucherId }, ct);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
