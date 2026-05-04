using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace decorativeplant_be.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxonomyIdToInventoryLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TaxonomyId",
                table: "inventory_location",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_location_TaxonomyId",
                table: "inventory_location",
                column: "TaxonomyId");

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_location_plant_taxonomy_TaxonomyId",
                table: "inventory_location",
                column: "TaxonomyId",
                principalTable: "plant_taxonomy",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_inventory_location_plant_taxonomy_TaxonomyId",
                table: "inventory_location");

            migrationBuilder.DropIndex(
                name: "IX_inventory_location_TaxonomyId",
                table: "inventory_location");

            migrationBuilder.DropColumn(
                name: "TaxonomyId",
                table: "inventory_location");
        }
    }
}
