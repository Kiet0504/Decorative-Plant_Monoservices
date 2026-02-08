using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace decorativeplant_be.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NewSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TaxCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Info = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlantCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Slug = table.Column<string>(type: "text", nullable: true),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IconUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlantCategories_PlantCategories_ParentId",
                        column: x => x.ParentId,
                        principalTable: "PlantCategories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "supplier",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    TaxCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ContactInfo = table.Column<string>(type: "jsonb", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "system_config",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "jsonb", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_config", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "user_account",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    EmailVerified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DisplayName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    AvatarUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Bio = table.Column<string>(type: "text", nullable: true),
                    LocationCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HardinessZone = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ExperienceLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Addresses = table.Column<string>(type: "jsonb", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_account", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "branch",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BranchType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ContactInfo = table.Column<string>(type: "jsonb", nullable: true),
                    OperatingHours = table.Column<string>(type: "jsonb", nullable: true),
                    Settings = table.Column<string>(type: "jsonb", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branch", x => x.Id);
                    table.ForeignKey(
                        name: "FK_branch_company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plant_taxonomy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ScientificName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CommonNames = table.Column<string>(type: "jsonb", nullable: true),
                    TaxonomyInfo = table.Column<string>(type: "jsonb", nullable: true),
                    CareInfo = table.Column<string>(type: "jsonb", nullable: true),
                    GrowthInfo = table.Column<string>(type: "jsonb", nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plant_taxonomy", x => x.Id);
                    table.ForeignKey(
                        name: "FK_plant_taxonomy_PlantCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "PlantCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ai_training_feedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    SourceInfo = table.Column<string>(type: "jsonb", nullable: true),
                    FeedbackContent = table.Column<string>(type: "jsonb", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_training_feedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_training_feedback_user_account_UserId",
                        column: x => x.UserId,
                        principalTable: "user_account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "notification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Body = table.Column<string>(type: "text", nullable: true),
                    Data = table.Column<string>(type: "jsonb", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notification_user_account_UserId",
                        column: x => x.UserId,
                        principalTable: "user_account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shopping_cart",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Items = table.Column<string>(type: "jsonb", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shopping_cart", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shopping_cart_user_account_UserId",
                        column: x => x.UserId,
                        principalTable: "user_account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "inventory_location",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Details = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_location", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_location_branch_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_inventory_location_inventory_location_ParentLocationId",
                        column: x => x.ParentLocationId,
                        principalTable: "inventory_location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "order_header",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OrderCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    TypeInfo = table.Column<string>(type: "jsonb", nullable: true),
                    Financials = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "jsonb", nullable: true),
                    DeliveryAddress = table.Column<string>(type: "jsonb", nullable: true),
                    PickupInfo = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_header", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_header_branch_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_order_header_user_account_UserId",
                        column: x => x.UserId,
                        principalTable: "user_account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "promotion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Config = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promotion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_promotion_branch_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "shipping_zone",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Locations = table.Column<string>(type: "jsonb", nullable: true),
                    FeeConfig = table.Column<string>(type: "jsonb", nullable: true),
                    DeliveryTimeConfig = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipping_zone", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shipping_zone_branch_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "staff_assignment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    StaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Permissions = table.Column<string>(type: "jsonb", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_assignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_assignment_branch_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_staff_assignment_user_account_StaffId",
                        column: x => x.StaffId,
                        principalTable: "user_account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "voucher",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Info = table.Column<string>(type: "jsonb", nullable: true),
                    Rules = table.Column<string>(type: "jsonb", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher", x => x.Id);
                    table.ForeignKey(
                        name: "FK_voucher_branch_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "garden_plant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaxonomyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Details = table.Column<string>(type: "jsonb", nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_garden_plant", x => x.Id);
                    table.ForeignKey(
                        name: "FK_garden_plant_plant_taxonomy_TaxonomyId",
                        column: x => x.TaxonomyId,
                        principalTable: "plant_taxonomy",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_garden_plant_user_account_UserId",
                        column: x => x.UserId,
                        principalTable: "user_account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "plant_batch",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaxonomyId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatchCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SourceInfo = table.Column<string>(type: "jsonb", nullable: true),
                    Specs = table.Column<string>(type: "jsonb", nullable: true),
                    InitialQuantity = table.Column<int>(type: "integer", nullable: true),
                    CurrentTotalQuantity = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plant_batch", x => x.Id);
                    table.ForeignKey(
                        name: "FK_plant_batch_branch_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_plant_batch_plant_batch_ParentBatchId",
                        column: x => x.ParentBatchId,
                        principalTable: "plant_batch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_plant_batch_plant_taxonomy_TaxonomyId",
                        column: x => x.TaxonomyId,
                        principalTable: "plant_taxonomy",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_plant_batch_supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "iot_device",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeviceInfo = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ActivityLog = table.Column<string>(type: "jsonb", nullable: true),
                    Components = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_iot_device", x => x.Id);
                    table.ForeignKey(
                        name: "FK_iot_device_branch_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_iot_device_inventory_location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "inventory_location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "payment_transaction",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransactionCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Details = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_transaction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_transaction_order_header_OrderId",
                        column: x => x.OrderId,
                        principalTable: "order_header",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "return_request",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Info = table.Column<string>(type: "jsonb", nullable: true),
                    Images = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_request", x => x.Id);
                    table.ForeignKey(
                        name: "FK_return_request_order_header_OrderId",
                        column: x => x.OrderId,
                        principalTable: "order_header",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_return_request_user_account_UserId",
                        column: x => x.UserId,
                        principalTable: "user_account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "shipping",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    TrackingCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CarrierInfo = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DeliveryDetails = table.Column<string>(type: "jsonb", nullable: true),
                    Events = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipping", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shipping_order_header_OrderId",
                        column: x => x.OrderId,
                        principalTable: "order_header",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "care_schedule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    GardenPlantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaskInfo = table.Column<string>(type: "jsonb", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_care_schedule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_care_schedule_garden_plant_GardenPlantId",
                        column: x => x.GardenPlantId,
                        principalTable: "garden_plant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plant_diagnosis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    GardenPlantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserInput = table.Column<string>(type: "jsonb", nullable: true),
                    AiResult = table.Column<string>(type: "jsonb", nullable: true),
                    Feedback = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plant_diagnosis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_plant_diagnosis_garden_plant_GardenPlantId",
                        column: x => x.GardenPlantId,
                        principalTable: "garden_plant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "batch_stock",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantities = table.Column<string>(type: "jsonb", nullable: true),
                    HealthStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LastCountInfo = table.Column<string>(type: "jsonb", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batch_stock", x => x.Id);
                    table.ForeignKey(
                        name: "FK_batch_stock_inventory_location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "inventory_location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_batch_stock_plant_batch_BatchId",
                        column: x => x.BatchId,
                        principalTable: "plant_batch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "cultivation_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActivityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Details = table.Column<string>(type: "jsonb", nullable: true),
                    PerformedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    PerformedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cultivation_log", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cultivation_log_inventory_location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "inventory_location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_cultivation_log_plant_batch_BatchId",
                        column: x => x.BatchId,
                        principalTable: "plant_batch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_cultivation_log_user_account_PerformedBy",
                        column: x => x.PerformedBy,
                        principalTable: "user_account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "product_listing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductInfo = table.Column<string>(type: "jsonb", nullable: true),
                    StatusInfo = table.Column<string>(type: "jsonb", nullable: true),
                    SeoInfo = table.Column<string>(type: "jsonb", nullable: true),
                    Images = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_listing", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_listing_branch_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_product_listing_plant_batch_BatchId",
                        column: x => x.BatchId,
                        principalTable: "plant_batch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "stock_transfer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TransferCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromBranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToBranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LogisticsInfo = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_transfer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_transfer_branch_FromBranchId",
                        column: x => x.FromBranchId,
                        principalTable: "branch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_stock_transfer_branch_ToBranchId",
                        column: x => x.ToBranchId,
                        principalTable: "branch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_stock_transfer_inventory_location_FromLocationId",
                        column: x => x.FromLocationId,
                        principalTable: "inventory_location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_stock_transfer_inventory_location_ToLocationId",
                        column: x => x.ToLocationId,
                        principalTable: "inventory_location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_stock_transfer_plant_batch_BatchId",
                        column: x => x.BatchId,
                        principalTable: "plant_batch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "automation_rule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Schedule = table.Column<string>(type: "jsonb", nullable: true),
                    Conditions = table.Column<string>(type: "jsonb", nullable: true),
                    Actions = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_rule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_automation_rule_iot_device_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "iot_device",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "IotAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ComponentKey = table.Column<string>(type: "text", nullable: true),
                    AlertInfo = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    ResolutionInfo = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IotAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IotAlerts_iot_device_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "iot_device",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "sensor_reading",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComponentKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Value = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_reading", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sensor_reading_iot_device_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "iot_device",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "care_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    GardenPlantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: true),
                    LogInfo = table.Column<string>(type: "jsonb", nullable: true),
                    Images = table.Column<string>(type: "jsonb", nullable: true),
                    PerformedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_care_log", x => x.Id);
                    table.ForeignKey(
                        name: "FK_care_log_care_schedule_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "care_schedule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_care_log_garden_plant_GardenPlantId",
                        column: x => x.GardenPlantId,
                        principalTable: "garden_plant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "health_incident",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    StockId = table.Column<Guid>(type: "uuid", nullable: true),
                    IncidentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Details = table.Column<string>(type: "jsonb", nullable: true),
                    TreatmentInfo = table.Column<string>(type: "jsonb", nullable: true),
                    StatusInfo = table.Column<string>(type: "jsonb", nullable: true),
                    Images = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_incident", x => x.Id);
                    table.ForeignKey(
                        name: "FK_health_incident_batch_stock_StockId",
                        column: x => x.StockId,
                        principalTable: "batch_stock",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_health_incident_plant_batch_BatchId",
                        column: x => x.BatchId,
                        principalTable: "plant_batch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "stock_adjustment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    StockId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    QuantityChange = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    MetaInfo = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_adjustment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_adjustment_batch_stock_StockId",
                        column: x => x.StockId,
                        principalTable: "batch_stock",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "order_item",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: true),
                    StockId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Pricing = table.Column<string>(type: "jsonb", nullable: true),
                    Snapshots = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_item", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_item_batch_stock_StockId",
                        column: x => x.StockId,
                        principalTable: "batch_stock",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_order_item_order_header_OrderId",
                        column: x => x.OrderId,
                        principalTable: "order_header",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_order_item_plant_batch_BatchId",
                        column: x => x.BatchId,
                        principalTable: "plant_batch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_order_item_product_listing_ListingId",
                        column: x => x.ListingId,
                        principalTable: "product_listing",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "product_review",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Content = table.Column<string>(type: "jsonb", nullable: true),
                    StatusInfo = table.Column<string>(type: "jsonb", nullable: true),
                    Images = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_review", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_review_order_header_OrderId",
                        column: x => x.OrderId,
                        principalTable: "order_header",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_product_review_product_listing_ListingId",
                        column: x => x.ListingId,
                        principalTable: "product_listing",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_review_user_account_UserId",
                        column: x => x.UserId,
                        principalTable: "user_account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "automation_execution_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    RuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExecutionInfo = table.Column<string>(type: "jsonb", nullable: true),
                    ExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_execution_log", x => x.Id);
                    table.ForeignKey(
                        name: "FK_automation_execution_log_automation_rule_RuleId",
                        column: x => x.RuleId,
                        principalTable: "automation_rule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_training_feedback_UserId",
                table: "ai_training_feedback",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_automation_execution_log_RuleId",
                table: "automation_execution_log",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_automation_rule_DeviceId",
                table: "automation_rule",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_batch_stock_BatchId",
                table: "batch_stock",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_batch_stock_LocationId",
                table: "batch_stock",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_branch_Code",
                table: "branch",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_branch_CompanyId",
                table: "branch",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_branch_Slug",
                table: "branch",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_care_log_GardenPlantId",
                table: "care_log",
                column: "GardenPlantId");

            migrationBuilder.CreateIndex(
                name: "IX_care_log_ScheduleId",
                table: "care_log",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_care_schedule_GardenPlantId",
                table: "care_schedule",
                column: "GardenPlantId");

            migrationBuilder.CreateIndex(
                name: "IX_cultivation_log_BatchId",
                table: "cultivation_log",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_cultivation_log_LocationId",
                table: "cultivation_log",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_cultivation_log_PerformedBy",
                table: "cultivation_log",
                column: "PerformedBy");

            migrationBuilder.CreateIndex(
                name: "IX_garden_plant_TaxonomyId",
                table: "garden_plant",
                column: "TaxonomyId");

            migrationBuilder.CreateIndex(
                name: "IX_garden_plant_UserId",
                table: "garden_plant",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_health_incident_BatchId",
                table: "health_incident",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_health_incident_StockId",
                table: "health_incident",
                column: "StockId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_location_BranchId",
                table: "inventory_location",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_location_ParentLocationId",
                table: "inventory_location",
                column: "ParentLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_iot_device_BranchId",
                table: "iot_device",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_iot_device_LocationId",
                table: "iot_device",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_IotAlerts_DeviceId",
                table: "IotAlerts",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_UserId",
                table: "notification",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_order_header_BranchId",
                table: "order_header",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_order_header_UserId",
                table: "order_header",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_order_item_BatchId",
                table: "order_item",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_order_item_ListingId",
                table: "order_item",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_order_item_OrderId",
                table: "order_item",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_order_item_StockId",
                table: "order_item",
                column: "StockId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transaction_OrderId",
                table: "payment_transaction",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_plant_batch_BranchId",
                table: "plant_batch",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_plant_batch_ParentBatchId",
                table: "plant_batch",
                column: "ParentBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_plant_batch_SupplierId",
                table: "plant_batch",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_plant_batch_TaxonomyId",
                table: "plant_batch",
                column: "TaxonomyId");

            migrationBuilder.CreateIndex(
                name: "IX_plant_diagnosis_GardenPlantId",
                table: "plant_diagnosis",
                column: "GardenPlantId");

            migrationBuilder.CreateIndex(
                name: "IX_plant_taxonomy_CategoryId",
                table: "plant_taxonomy",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_plant_taxonomy_ScientificName",
                table: "plant_taxonomy",
                column: "ScientificName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlantCategories_ParentId",
                table: "PlantCategories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_product_listing_BatchId",
                table: "product_listing",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_product_listing_BranchId",
                table: "product_listing",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_product_review_ListingId",
                table: "product_review",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_product_review_OrderId",
                table: "product_review",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_product_review_UserId",
                table: "product_review",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_promotion_BranchId",
                table: "promotion",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_return_request_OrderId",
                table: "return_request",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_return_request_UserId",
                table: "return_request",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_sensor_reading_DeviceId",
                table: "sensor_reading",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_shipping_OrderId",
                table: "shipping",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_shipping_zone_BranchId",
                table: "shipping_zone",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_shopping_cart_UserId",
                table: "shopping_cart",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_assignment_BranchId",
                table: "staff_assignment",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_assignment_StaffId",
                table: "staff_assignment",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_adjustment_StockId",
                table: "stock_adjustment",
                column: "StockId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_BatchId",
                table: "stock_transfer",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_FromBranchId",
                table: "stock_transfer",
                column: "FromBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_FromLocationId",
                table: "stock_transfer",
                column: "FromLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_ToBranchId",
                table: "stock_transfer",
                column: "ToBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_ToLocationId",
                table: "stock_transfer",
                column: "ToLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_user_account_Email",
                table: "user_account",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_account_Phone",
                table: "user_account",
                column: "Phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_voucher_BranchId",
                table: "voucher",
                column: "BranchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_training_feedback");

            migrationBuilder.DropTable(
                name: "automation_execution_log");

            migrationBuilder.DropTable(
                name: "care_log");

            migrationBuilder.DropTable(
                name: "cultivation_log");

            migrationBuilder.DropTable(
                name: "health_incident");

            migrationBuilder.DropTable(
                name: "IotAlerts");

            migrationBuilder.DropTable(
                name: "notification");

            migrationBuilder.DropTable(
                name: "order_item");

            migrationBuilder.DropTable(
                name: "payment_transaction");

            migrationBuilder.DropTable(
                name: "plant_diagnosis");

            migrationBuilder.DropTable(
                name: "product_review");

            migrationBuilder.DropTable(
                name: "promotion");

            migrationBuilder.DropTable(
                name: "return_request");

            migrationBuilder.DropTable(
                name: "sensor_reading");

            migrationBuilder.DropTable(
                name: "shipping");

            migrationBuilder.DropTable(
                name: "shipping_zone");

            migrationBuilder.DropTable(
                name: "shopping_cart");

            migrationBuilder.DropTable(
                name: "staff_assignment");

            migrationBuilder.DropTable(
                name: "stock_adjustment");

            migrationBuilder.DropTable(
                name: "stock_transfer");

            migrationBuilder.DropTable(
                name: "system_config");

            migrationBuilder.DropTable(
                name: "voucher");

            migrationBuilder.DropTable(
                name: "automation_rule");

            migrationBuilder.DropTable(
                name: "care_schedule");

            migrationBuilder.DropTable(
                name: "product_listing");

            migrationBuilder.DropTable(
                name: "order_header");

            migrationBuilder.DropTable(
                name: "batch_stock");

            migrationBuilder.DropTable(
                name: "iot_device");

            migrationBuilder.DropTable(
                name: "garden_plant");

            migrationBuilder.DropTable(
                name: "plant_batch");

            migrationBuilder.DropTable(
                name: "inventory_location");

            migrationBuilder.DropTable(
                name: "user_account");

            migrationBuilder.DropTable(
                name: "plant_taxonomy");

            migrationBuilder.DropTable(
                name: "supplier");

            migrationBuilder.DropTable(
                name: "branch");

            migrationBuilder.DropTable(
                name: "PlantCategories");

            migrationBuilder.DropTable(
                name: "company");
        }
    }
}
