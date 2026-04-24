using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Common.Interfaces;

/// <summary>
/// Abstraction over ApplicationDbContext so the Application layer doesn't depend on Infrastructure.
/// </summary>
public interface IApplicationDbContext
{
   
    // Module 1: Identity & Access
    DbSet<UserAccount> UserAccounts { get; }
    DbSet<UserSubscription> UserSubscriptions { get; }
    DbSet<FeatureUsageQuota> FeatureUsageQuotas { get; }
    DbSet<PremiumFeature> PremiumFeatures { get; }

    // Module 5: Commerce
    DbSet<ProductListing> ProductListings { get; }
    DbSet<ShippingZone> ShippingZones { get; }
    DbSet<Voucher> Vouchers { get; }
    DbSet<Promotion> Promotions { get; }
    DbSet<ShoppingCart> ShoppingCarts { get; }
    DbSet<WishlistItem> WishlistItems { get; }
    DbSet<OrderHeader> OrderHeaders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<PaymentTransaction> PaymentTransactions { get; }
    DbSet<Shipping> Shippings { get; }

    // Module 7: Reviews
    DbSet<ProductReview> ProductReviews { get; }

    // Module 5: Returns
    DbSet<ReturnRequest> ReturnRequests { get; }

    // Module 4: Inventory
    DbSet<PlantCategory> PlantCategories { get; }
    DbSet<BatchStock> BatchStocks { get; }
    DbSet<InventoryLocation> InventoryLocations { get; }
    DbSet<PlantBatch> PlantBatches { get; }
    DbSet<PlantTaxonomy> PlantTaxonomies { get; }
    DbSet<StockTransfer> StockTransfers { get; }
    DbSet<CultivationLog> CultivationLogs { get; }
    DbSet<HealthIncident> HealthIncidents { get; }
    
    // Admin / Core
    DbSet<Company> Companies { get; }
    DbSet<Branch> Branches { get; }
    DbSet<StaffAssignment> StaffAssignments { get; }
    
    // Module 4: IoT & Automation
    DbSet<IotDevice> IotDevices { get; }
    DbSet<SensorReading> SensorReadings { get; }
    DbSet<AutomationRule> AutomationRules { get; }
    DbSet<IotAlert> IotAlerts { get; }

    // Module 6: My Garden
    DbSet<GardenPlant> GardenPlants { get; }
    DbSet<CareLog> CareLogs { get; }
    DbSet<CareSchedule> CareSchedules { get; }

    // Recommendations
    DbSet<RecommendationLog> RecommendationLogs { get; }

    // Module 9: AR Preview
    DbSet<ArPreviewSession> ArPreviewSessions { get; }
    DbSet<ProductModelAsset> ProductModelAssets { get; }

    // Module 10: AI Assistant chat history
    DbSet<AiChatThread> AiChatThreads { get; }
    DbSet<AiChatMessage> AiChatMessages { get; }

    Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }
    
    /// <summary>
    /// Acquires a PostgreSQL row-level lock (FOR UPDATE) on the ProductListing row 
    /// for the given listingId. Must be called within a transaction scope.
    /// This prevents race conditions during concurrent stock reservations.
    /// </summary>
    Task AcquireStockLockAsync(Guid listingId, CancellationToken ct = default);

    /// <summary>
    /// Acquires a PostgreSQL row-level lock (FOR UPDATE) on the Voucher row
    /// for the given voucherId. Must be called within a transaction scope.
    /// Prevents concurrent checkouts from both incrementing <c>used_count</c>
    /// past <c>usage_limit</c>.
    /// </summary>
    Task AcquireVoucherLockAsync(Guid voucherId, CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
