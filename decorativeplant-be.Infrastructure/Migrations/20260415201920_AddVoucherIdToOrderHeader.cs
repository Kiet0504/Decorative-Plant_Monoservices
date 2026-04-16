using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace decorativeplant_be.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherIdToOrderHeader : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VoucherId",
                table: "order_header",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_header_VoucherId",
                table: "order_header",
                column: "VoucherId");

            migrationBuilder.AddForeignKey(
                name: "FK_order_header_voucher_VoucherId",
                table: "order_header",
                column: "VoucherId",
                principalTable: "voucher",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_order_header_voucher_VoucherId",
                table: "order_header");

            migrationBuilder.DropIndex(
                name: "IX_order_header_VoucherId",
                table: "order_header");

            migrationBuilder.DropColumn(
                name: "VoucherId",
                table: "order_header");
        }
    }
}
