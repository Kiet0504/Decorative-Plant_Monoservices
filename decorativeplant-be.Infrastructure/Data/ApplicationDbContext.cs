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

    // Module 1: Identity
    // UserAccount (inherited from IdentityUser)
    public DbSet<Notification> Notifications { get; set; }

    // Module 2: Company Structure
    public DbSet<Company> Companies { get; set; }
    public DbSet<Branch> Branches { get; set; }
    public DbSet<StaffAssignment> StaffAssignments { get; set; }

    // Module 3: Inventory & Cultivation
    public DbSet<PlantTaxonomy> PlantTaxonomies { get; set; }
    public DbSet<PlantCategory> PlantCategories { get; set; }
    public DbSet<InventoryLocation> InventoryLocations { get; set; }
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<PlantBatch> PlantBatches { get; set; }
    public DbSet<BatchStock> BatchStocks { get; set; }
    public DbSet<StockAdjustment> StockAdjustments { get; set; }
    public DbSet<StockTransfer> StockTransfers { get; set; }
    public DbSet<CultivationLog> CultivationLogs { get; set; }
    public DbSet<HealthIncident> HealthIncidents { get; set; }

    // Module 4: IoT & Automation
    public DbSet<IoTDevice> IoTDevices { get; set; }
    public DbSet<SensorReading> SensorReadings { get; set; }
    public DbSet<AutomationRule> AutomationRules { get; set; }
    public DbSet<AutomationExecutionLog> AutomationExecutionLogs { get; set; }
    public DbSet<IoTAlert> IoTAlerts { get; set; }

    // Module 5: Commerce
    public DbSet<ProductListing> ProductListings { get; set; }
    public DbSet<ShippingZone> ShippingZones { get; set; }
    public DbSet<Voucher> Vouchers { get; set; }
    public DbSet<Promotion> Promotions { get; set; }
    public DbSet<ShoppingCart> ShoppingCarts { get; set; }
    public DbSet<OrderHeader> Orders { get; set; } // Map to order_header
    public DbSet<OrderItem> OrderItems { get; set; } // Map to order_item
    public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
    public DbSet<Shipping> Shippings { get; set; }
    public DbSet<ReturnRequest> ReturnRequests { get; set; }

    // Module 6: My Garden
    public DbSet<GardenPlant> GardenPlants { get; set; }
    public DbSet<CareSchedule> CareSchedules { get; set; }
    public DbSet<CareLog> CareLogs { get; set; }
    public DbSet<PlantDiagnosis> PlantDiagnoses { get; set; }

    // Module 7 & 8
    public DbSet<ProductReview> ProductReviews { get; set; }
    public DbSet<AiTrainingFeedback> AiTrainingFeedbacks { get; set; }
    public DbSet<SystemConfig> SystemConfigs { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasPostgresExtension("postgis"); // Keep postgis if used, though DBML didn't specify, existing had it.

        // Renaming Identity tables to snake_case
        builder.Entity<UserAccount>().ToTable("user_account");
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

        // Module 1
        builder.Entity<Notification>().ToTable("notification");
        builder.Entity<Notification>().Property(x => x.Data).HasColumnType("jsonb");
        
        // Configure UserAccount Addresses (JSONB)
        builder.Entity<UserAccount>().OwnsMany(u => u.Addresses, a => {
            a.ToJson(); // EF Core 7+ Map to JSON column
        });

        // Module 2
        builder.Entity<Company>().ToTable("company");
        builder.Entity<Company>().Property(x => x.Info).HasColumnType("jsonb");

        builder.Entity<Branch>().ToTable("branch");
        builder.Entity<Branch>().OwnsOne(x => x.ContactInfo, cb => {
             cb.ToJson(); // Map ContactInfo object to JSON
        });
        builder.Entity<Branch>().Property(x => x.OperatingHours).HasColumnType("jsonb");
        builder.Entity<Branch>().Property(x => x.Settings).HasColumnType("jsonb");

        builder.Entity<StaffAssignment>().ToTable("staff_assignment");
        builder.Entity<StaffAssignment>().Property(x => x.Permissions).HasColumnType("jsonb");

        // Module 3
        builder.Entity<PlantTaxonomy>().ToTable("plant_taxonomy");
        builder.Entity<PlantTaxonomy>().Property(x => x.CommonNames).HasColumnType("jsonb");
        builder.Entity<PlantTaxonomy>().Property(x => x.TaxonomyInfo).HasColumnType("jsonb");
        builder.Entity<PlantTaxonomy>().Property(x => x.CareInfo).HasColumnType("jsonb");
        builder.Entity<PlantTaxonomy>().Property(x => x.GrowthInfo).HasColumnType("jsonb");

        builder.Entity<PlantCategory>().ToTable("plant_category");

        builder.Entity<InventoryLocation>().ToTable("inventory_location");
        builder.Entity<InventoryLocation>().Property(x => x.Details).HasColumnType("jsonb");

        builder.Entity<Supplier>().ToTable("supplier");
        builder.Entity<Supplier>().Property(x => x.ContactInfo).HasColumnType("jsonb");

        builder.Entity<PlantBatch>().ToTable("plant_batch");
        builder.Entity<PlantBatch>().Property(x => x.SourceInfo).HasColumnType("jsonb");
        builder.Entity<PlantBatch>().Property(x => x.Specs).HasColumnType("jsonb");

        builder.Entity<BatchStock>().ToTable("batch_stock");
        builder.Entity<BatchStock>().Property(x => x.Quantities).HasColumnType("jsonb");
        builder.Entity<BatchStock>().Property(x => x.LastCountInfo).HasColumnType("jsonb");

        builder.Entity<StockAdjustment>().ToTable("stock_adjustment");
        builder.Entity<StockAdjustment>().Property(x => x.MetaInfo).HasColumnType("jsonb");

        builder.Entity<StockTransfer>().ToTable("stock_transfer");
        builder.Entity<StockTransfer>().Property(x => x.LogisticsInfo).HasColumnType("jsonb");

        builder.Entity<CultivationLog>().ToTable("cultivation_log");
        builder.Entity<CultivationLog>().Property(x => x.Details).HasColumnType("jsonb");

        builder.Entity<HealthIncident>().ToTable("health_incident");
        builder.Entity<HealthIncident>().Property(x => x.Details).HasColumnType("jsonb");
        builder.Entity<HealthIncident>().Property(x => x.TreatmentInfo).HasColumnType("jsonb");
        builder.Entity<HealthIncident>().Property(x => x.StatusInfo).HasColumnType("jsonb");
        builder.Entity<HealthIncident>().Property(x => x.Images).HasColumnType("jsonb");

        // Module 4
        builder.Entity<IoTDevice>().ToTable("iot_device");
        builder.Entity<IoTDevice>().Property(x => x.DeviceInfo).HasColumnType("jsonb");
        builder.Entity<IoTDevice>().Property(x => x.ActivityLog).HasColumnType("jsonb");
        builder.Entity<IoTDevice>().Property(x => x.Components).HasColumnType("jsonb");

        builder.Entity<SensorReading>().ToTable("sensor_reading");

        builder.Entity<AutomationRule>().ToTable("automation_rule");
        builder.Entity<AutomationRule>().Property(x => x.Schedule).HasColumnType("jsonb");
        builder.Entity<AutomationRule>().Property(x => x.Conditions).HasColumnType("jsonb");
        builder.Entity<AutomationRule>().Property(x => x.Actions).HasColumnType("jsonb");

        builder.Entity<AutomationExecutionLog>().ToTable("automation_execution_log");
        builder.Entity<AutomationExecutionLog>().Property(x => x.ExecutionInfo).HasColumnType("jsonb");

        builder.Entity<IoTAlert>().ToTable("iot_alert");
        builder.Entity<IoTAlert>().Property(x => x.AlertInfo).HasColumnType("jsonb");
        builder.Entity<IoTAlert>().Property(x => x.ResolutionInfo).HasColumnType("jsonb");

        // Module 5
        builder.Entity<ProductListing>().ToTable("product_listing");
        builder.Entity<ProductListing>().Property(x => x.ProductInfo).HasColumnType("jsonb");
        builder.Entity<ProductListing>().Property(x => x.StatusInfo).HasColumnType("jsonb");
        builder.Entity<ProductListing>().Property(x => x.SeoInfo).HasColumnType("jsonb");
        builder.Entity<ProductListing>().Property(x => x.Images).HasColumnType("jsonb");

        builder.Entity<ShippingZone>().ToTable("shipping_zone");
        builder.Entity<ShippingZone>().Property(x => x.Locations).HasColumnType("jsonb");
        builder.Entity<ShippingZone>().Property(x => x.FeeConfig).HasColumnType("jsonb");
        builder.Entity<ShippingZone>().Property(x => x.DeliveryTimeConfig).HasColumnType("jsonb");

        builder.Entity<Voucher>().ToTable("voucher");
        builder.Entity<Voucher>().Property(x => x.Info).HasColumnType("jsonb");
        builder.Entity<Voucher>().Property(x => x.Rules).HasColumnType("jsonb");

        builder.Entity<Promotion>().ToTable("promotion");
        builder.Entity<Promotion>().Property(x => x.Config).HasColumnType("jsonb");

        builder.Entity<ShoppingCart>().ToTable("shopping_cart");
        builder.Entity<ShoppingCart>().Property(x => x.Items).HasColumnType("jsonb");

        builder.Entity<OrderHeader>().ToTable("order_header");
        builder.Entity<OrderHeader>().Property(x => x.TypeInfo).HasColumnType("jsonb");
        builder.Entity<OrderHeader>().Property(x => x.Financials).HasColumnType("jsonb");
        builder.Entity<OrderHeader>().Property(x => x.Notes).HasColumnType("jsonb");
        builder.Entity<OrderHeader>().Property(x => x.DeliveryAddress).HasColumnType("jsonb");
        builder.Entity<OrderHeader>().Property(x => x.PickupInfo).HasColumnType("jsonb");

        builder.Entity<OrderItem>().ToTable("order_item");
        builder.Entity<OrderItem>().Property(x => x.Pricing).HasColumnType("jsonb");
        builder.Entity<OrderItem>().Property(x => x.Snapshots).HasColumnType("jsonb");

        builder.Entity<PaymentTransaction>().ToTable("payment_transaction");
        builder.Entity<PaymentTransaction>().Property(x => x.Details).HasColumnType("jsonb");

        builder.Entity<Shipping>().ToTable("shipping");
        builder.Entity<Shipping>().Property(x => x.CarrierInfo).HasColumnType("jsonb");
        builder.Entity<Shipping>().Property(x => x.DeliveryDetails).HasColumnType("jsonb");
        builder.Entity<Shipping>().Property(x => x.Events).HasColumnType("jsonb");

        builder.Entity<ReturnRequest>().ToTable("return_request");
        builder.Entity<ReturnRequest>().Property(x => x.Info).HasColumnType("jsonb");
        builder.Entity<ReturnRequest>().Property(x => x.Images).HasColumnType("jsonb");

        // Module 6
        builder.Entity<GardenPlant>().ToTable("garden_plant");
        builder.Entity<GardenPlant>().Property(x => x.Details).HasColumnType("jsonb");

        builder.Entity<CareSchedule>().ToTable("care_schedule");
        builder.Entity<CareSchedule>().Property(x => x.TaskInfo).HasColumnType("jsonb");

        builder.Entity<CareLog>().ToTable("care_log");
        builder.Entity<CareLog>().Property(x => x.LogInfo).HasColumnType("jsonb");
        builder.Entity<CareLog>().Property(x => x.Images).HasColumnType("jsonb");

        builder.Entity<PlantDiagnosis>().ToTable("plant_diagnosis");
        builder.Entity<PlantDiagnosis>().Property(x => x.UserInput).HasColumnType("jsonb");
        builder.Entity<PlantDiagnosis>().Property(x => x.AiResult).HasColumnType("jsonb");
        builder.Entity<PlantDiagnosis>().Property(x => x.Feedback).HasColumnType("jsonb");

        // Module 7 & 8
        builder.Entity<ProductReview>().ToTable("product_review");
        builder.Entity<ProductReview>().Property(x => x.Content).HasColumnType("jsonb");
        builder.Entity<ProductReview>().Property(x => x.StatusInfo).HasColumnType("jsonb");
        builder.Entity<ProductReview>().Property(x => x.Images).HasColumnType("jsonb");

        builder.Entity<AiTrainingFeedback>().ToTable("ai_training_feedback");
        builder.Entity<AiTrainingFeedback>().Property(x => x.SourceInfo).HasColumnType("jsonb");
        builder.Entity<AiTrainingFeedback>().Property(x => x.FeedbackContent).HasColumnType("jsonb");

        builder.Entity<SystemConfig>().ToTable("system_config");
        builder.Entity<SystemConfig>().Property(x => x.Value).HasColumnType("jsonb");
   
        // Relationships
        builder.Entity<PlantBatch>()
            .HasOne(pb => pb.ParentBatch)
            .WithMany()
            .HasForeignKey(pb => pb.ParentBatchId);

        builder.Entity<InventoryLocation>()
            .HasOne(il => il.ParentLocation)
            .WithMany()
            .HasForeignKey(il => il.ParentLocationId);
            
         // Prevent cascading deletes/cycles where appropriate
         builder.Entity<OrderHeader>()
            .HasOne(o => o.User)
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
