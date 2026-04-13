using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace decorativeplant_be.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArPreviewMvp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ar_preview_session",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScanJson = table.Column<string>(type: "jsonb", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TokenSalt = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ar_preview_session", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "product_model_asset",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ProductListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    GlbUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    DefaultScale = table.Column<decimal>(type: "numeric(10,4)", nullable: false, defaultValue: 1m),
                    BoundingBox = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_model_asset", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_model_asset_product_listing_ProductListingId",
                        column: x => x.ProductListingId,
                        principalTable: "product_listing",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ar_preview_session_ExpiresAt",
                table: "ar_preview_session",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_product_model_asset_ProductListingId",
                table: "product_model_asset",
                column: "ProductListingId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ar_preview_session");

            migrationBuilder.DropTable(
                name: "product_model_asset");
        }
    }
}
