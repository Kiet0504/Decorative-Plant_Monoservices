using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace decorativeplant_be.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSubscriptionAndFeatureTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "user_account",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "plant_taxonomy",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "notification",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "company",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "branch",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "feature_usage_quota",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    QuotaLimit = table.Column<int>(type: "integer", nullable: false),
                    QuotaUsed = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    QuotaPeriod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_usage_quota", x => x.Id);
                    table.ForeignKey(
                        name: "FK_feature_usage_quota_user_account_UserId",
                        column: x => x.UserId,
                        principalTable: "user_account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "premium_feature",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FeatureKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FeatureName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AvailableInPlans = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_premium_feature", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_subscription",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AutoRenew = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PaymentMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AmountPaid = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BillingCycle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_subscription", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_subscription_user_account_UserId",
                        column: x => x.UserId,
                        principalTable: "user_account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_feature_usage_quota_FeatureKey",
                table: "feature_usage_quota",
                column: "FeatureKey");

            migrationBuilder.CreateIndex(
                name: "IX_feature_usage_quota_UserId",
                table: "feature_usage_quota",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_feature_usage_quota_UserId_FeatureKey",
                table: "feature_usage_quota",
                columns: new[] { "UserId", "FeatureKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_premium_feature_FeatureKey",
                table: "premium_feature",
                column: "FeatureKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_premium_feature_IsActive",
                table: "premium_feature",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscription_Status",
                table: "user_subscription",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscription_UserId",
                table: "user_subscription",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscription_UserId_Status",
                table: "user_subscription",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feature_usage_quota");

            migrationBuilder.DropTable(
                name: "premium_feature");

            migrationBuilder.DropTable(
                name: "user_subscription");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "plant_taxonomy");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "notification");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "company");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "branch");
        }
    }
}
