using decorativeplant_be.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<UserAccount, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // User & Store
    public DbSet<UserProfile> UserProfiles { get; set; }
    public DbSet<Store> Stores { get; set; }
    public DbSet<StoreAddress> StoreAddresses { get; set; }

    // Plant Information & Inventory
    public DbSet<PlantTaxonomy> PlantTaxonomies { get; set; }
    public DbSet<PlantBatch> PlantBatches { get; set; }
    public DbSet<BatchStock> BatchStocks { get; set; }
    public DbSet<BatchLog> BatchLogs { get; set; }
    public DbSet<InventoryLocation> InventoryLocations { get; set; }
    public DbSet<InventoryAdjustment> InventoryAdjustments { get; set; }

    // Sales
    public DbSet<Listing> Listings { get; set; }
    public DbSet<ShoppingCart> ShoppingCarts { get; set; }
    public DbSet<Voucher> Vouchers { get; set; }
    public DbSet<ProductReview> ProductReviews { get; set; }
    public DbSet<OrderHeader> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
    public DbSet<Shipping> Shippings { get; set; }
    public DbSet<ShippingAddressSnapshot> ShippingAddressSnapshots { get; set; }

    // IoT
    public DbSet<IoTDevice> IoTDevices { get; set; }
    public DbSet<SensorReading> SensorReadings { get; set; }
    public DbSet<AutoRule> AutoRules { get; set; }
    public DbSet<RuleExecutionLog> RuleExecutionLogs { get; set; }

    // Care
    public DbSet<MyGardenPlant> MyGardenPlants { get; set; }
    public DbSet<CareSchedule> CareSchedules { get; set; }
    public DbSet<CareLog> CareLogs { get; set; }
    public DbSet<DiagnosisLog> DiagnosisLogs { get; set; }
    public DbSet<Notification> Notifications { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Rename Identity tables (Optional but standard for postgres sometimes to use snake_case)
        builder.HasPostgresExtension("postgis");
        builder.Entity<UserAccount>().ToTable("user_account");
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

        // Relationships and Configurations

        // PlantBatch Self-Referencing (ParentBatch)
        builder.Entity<PlantBatch>()
            .HasOne(pb => pb.ParentBatch)
            .WithMany()
            .HasForeignKey(pb => pb.ParentBatchId)
            .OnDelete(DeleteBehavior.Restrict);

        // InventoryLocation Self-Referencing (ParentLocation)
        builder.Entity<InventoryLocation>()
            .HasOne(il => il.ParentLocation)
            .WithMany()
            .HasForeignKey(il => il.ParentLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        // BatchStock Constraints
        builder.Entity<BatchStock>()
            .HasIndex(bs => new { bs.BatchId, bs.LocationId })
            .IsUnique();
            
        // One-to-One Relationships
        builder.Entity<UserProfile>()
            .HasOne(up => up.User)
            .WithOne()
            .HasForeignKey<UserProfile>(up => up.UserId);

        builder.Entity<ShoppingCart>()
            .HasOne(sc => sc.User)
            .WithOne()
            .HasForeignKey<ShoppingCart>(sc => sc.UserId);
            
        builder.Entity<Store>()
            .HasOne(s => s.OwnerUser)
            .WithMany()
            .HasForeignKey(s => s.OwnerUserId);

        // Prevent cascading delete for Orders
        builder.Entity<OrderHeader>()
            .HasOne(o => o.User)
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<OrderHeader>()
            .HasOne(o => o.Store)
            .WithMany()
            .HasForeignKey(o => o.StoreId)
            .OnDelete(DeleteBehavior.Restrict);
            
        // JSON Property Configuration
        builder.Entity<UserProfile>().Property(x => x.AddressJson).HasColumnType("jsonb");
        builder.Entity<UserProfile>().Property(x => x.PreferencesJson).HasColumnType("jsonb");
        builder.Entity<ShoppingCart>().Property(x => x.ItemsJson).HasColumnType("jsonb");
        builder.Entity<Listing>().Property(x => x.PhotosJson).HasColumnType("jsonb");
        builder.Entity<ProductReview>().Property(x => x.ImagesJson).HasColumnType("jsonb");
        builder.Entity<OrderHeader>().Property(x => x.StatusTimelineJson).HasColumnType("jsonb");
        builder.Entity<PaymentTransaction>().Property(x => x.PayloadJson).HasColumnType("jsonb");
        builder.Entity<Shipping>().Property(x => x.EventsJson).HasColumnType("jsonb");
        builder.Entity<ShippingAddressSnapshot>().Property(x => x.AddressJson).HasColumnType("jsonb");
        builder.Entity<IoTDevice>().Property(x => x.ConfigJson).HasColumnType("jsonb");
        builder.Entity<AutoRule>().Property(x => x.ActionPayloadJson).HasColumnType("jsonb");
        builder.Entity<RuleExecutionLog>().Property(x => x.DetailsJson).HasColumnType("jsonb");
        builder.Entity<CareSchedule>(); // GuideContentJson removed
        builder.Entity<CareLog>().Property(x => x.ImagesJson).HasColumnType("jsonb");
        builder.Entity<Notification>().Property(x => x.PayloadJson).HasColumnType("jsonb");
        builder.Entity<DiagnosisLog>().Property(x => x.AiResultJson).HasColumnType("jsonb");
        builder.Entity<DiagnosisLog>().Property(x => x.UserFeedbackJson).HasColumnType("jsonb");
    }
}
