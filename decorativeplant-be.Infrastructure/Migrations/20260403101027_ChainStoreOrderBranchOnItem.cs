using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace decorativeplant_be.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChainStoreOrderBranchOnItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_order_header_branch_BranchId",
                table: "order_header");

            migrationBuilder.DropIndex(
                name: "IX_order_header_BranchId",
                table: "order_header");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "order_header");

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "order_item",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_item_BranchId",
                table: "order_item",
                column: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_order_item_branch_BranchId",
                table: "order_item",
                column: "BranchId",
                principalTable: "branch",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_order_item_branch_BranchId",
                table: "order_item");

            migrationBuilder.DropIndex(
                name: "IX_order_item_BranchId",
                table: "order_item");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "order_item");

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "order_header",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_header_BranchId",
                table: "order_header",
                column: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_order_header_branch_BranchId",
                table: "order_header",
                column: "BranchId",
                principalTable: "branch",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
