using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Common.Interfaces;

/// <summary>
/// Abstraction over ApplicationDbContext so the Application layer doesn't depend on Infrastructure.
/// </summary>
public interface IApplicationDbContext
{
    // Module 1: Identity & Access
    DbSet<UserSubscription> UserSubscriptions { get; }
    DbSet<FeatureUsageQuota> FeatureUsageQuotas { get; }
    DbSet<PremiumFeature> PremiumFeatures { get; }

    // Module 5: Commerce
    DbSet<ProductListing> ProductListings { get; }
    DbSet<ShippingZone> ShippingZones { get; }
    DbSet<Voucher> Vouchers { get; }
    DbSet<Promotion> Promotions { get; }
    DbSet<ShoppingCart> ShoppingCarts { get; }
    DbSet<OrderHeader> OrderHeaders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<PaymentTransaction> PaymentTransactions { get; }
    DbSet<Shipping> Shippings { get; }

    // Module 7: Reviews
    DbSet<ProductReview> ProductReviews { get; }

    // Module 4: Inventory
    DbSet<BatchStock> BatchStocks { get; }
    DbSet<InventoryLocation> InventoryLocations { get; }
    
    // Admin / Core
    DbSet<Branch> Branches { get; }

    Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
