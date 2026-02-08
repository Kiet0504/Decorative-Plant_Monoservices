# Migration Plan: Old DB → New DB (PostgresDB.dbml)

## Summary of Differences

| Aspect | Old DB (Current Code) | New DB (PostgresDB.dbml) |
|--------|------------------------|---------------------------|
| **Business model** | Store-centric (marketplace: owners have stores) | Company → Branch (multi-branch nursery chain) |
| **User** | user_account + user_profile + user_address | user_account (merged profile + addresses JSONB) |
| **Organization** | Store, StoreAddress, StoreWallet, SellerPackage | Company, Branch, StaffAssignment |
| **Plant taxonomy** | Flat columns (CommonName, Cultivar, Family, etc.) | JSONB (common_names, taxonomy_info, care_info, growth_info) |
| **Inventory** | PlantBatch → BatchStock (StoreId) | PlantBatch → BatchStock (branch_id) + stock_adjustment, stock_transfer |
| **IoT** | IotDevice → SensorReading (component_id) | IotDevice (components JSONB) → SensorReading (device_id + component_key) |
| **Commerce** | Listing (Store + StockId) | product_listing (branch + batch_id), product_info/status_info JSONB |
| **Orders** | OrderHeader (StoreId), separate address tables | order_header (branch_id), delivery_address/pickup_info JSONB |
| **My Garden** | MyGardenPlant (SourceOrderItemId required) | garden_plant (details JSONB, source optional) |

---

## Migration Strategy

### Option A: Clean slate (recommended if no production data)

1. **Remove old migrations**
   - Delete `20260117112223_InitialSchema.cs` and `.Designer.cs`
   - Delete or reset `ApplicationDbContextModelSnapshot.cs`
   - Ensure DB is fresh or drop and recreate

2. **Update Domain entities**
   - Remove: Store, StoreAddress, StoreWallet, WalletTransaction, PlatformFeePolicy, SellerPackage, SellerSubscription
   - Add: Company, Branch, StaffAssignment
   - Update: UserAccount (merge profile + addresses JSONB), PlantTaxonomy (JSONB), etc.
   - Map all entities per `PostgresDB.dbml`

3. **Update Configurations**
   - Remove old `*Configuration.cs` files
   - Add new EF configurations for new schema
   - Add `HasColumnType("jsonb")` and `HasConversion<JsonDocument>()` for JSONB fields

4. **Create new migration**
   - `dotnet ef migrations add NewSchema -p decorativeplant-be.Infrastructure -s decorativeplant-be.API`

5. **Update Repositories, Services, Handlers**
   - Replace Store references with Branch/Company
   - Update queries and DTOs
   - Update UserAccountService, etc.

### Option B: Data migration (if you have production data)

1. Create new migration that:
   - Adds new tables (company, branch, staff_assignment, etc.)
   - Migrates Store → Company + Branch
   - Migrates user_profile + user_address → user_account JSONB
   - Migrates data for plant_taxonomy, batch, listing, order, etc.
2. Drop old tables after verification
3. Update code

---

## Files to Modify

### Domain (Entities)

- **Delete:** Store, StoreAddress, StoreWallet, WalletTransaction, PlatformFeePolicy, SellerPackage, SellerSubscription
- **Add:** Company, Branch, StaffAssignment, Supplier, PlantCategory, StockAdjustment, StockTransfer, CultivationLog, HealthIncident, GardenPlant, Promotion, ReturnRequest, AiTrainingFeedback, etc.
- **Update:** UserAccount (merged fields + addresses JSONB), PlantTaxonomy (JSONB), BatchStock, IotDevice, Listing → ProductListing, OrderHeader, OrderItem, etc.

### Infrastructure

- **ApplicationDbContext.cs** — Replace DbSets
- **Configurations/** — Delete old, add new
- **Migrations/** — Remove old, add new

### Application

- **Features/** — Update handlers and DTOs
- **Services/** — Update UserAccountService, etc.

---

## JSONB Handling in EF Core

For each JSONB property, use owned type or conversion:

```csharp
// Option 1: JsonDocument (flexible)
modelBuilder.Entity<UserAccount>()
    .Property(e => e.Addresses)
    .HasColumnType("jsonb")
    .HasConversion(
        v => JsonSerializer.Serialize(v),
        v => JsonDocument.Parse(v).RootElement);

// Option 2: Owned type (EF Core 7+)
modelBuilder.Entity<UserAccount>()
    .OwnsOne(e => e.AddressesData, a => {
        a.ToJson();
        a.Property(x => x.Items).HasColumnName("addresses");
    });
```

Always document JSONB structure in `docs/JSONB_SCHEMA_REFERENCE.md`.
