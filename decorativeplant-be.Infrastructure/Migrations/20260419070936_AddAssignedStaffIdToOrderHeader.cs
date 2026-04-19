using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace decorativeplant_be.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignedStaffIdToOrderHeader : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedStaffId",
                table: "order_header",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_header_AssignedStaffId",
                table: "order_header",
                column: "AssignedStaffId");

            migrationBuilder.AddForeignKey(
                name: "FK_order_header_user_account_AssignedStaffId",
                table: "order_header",
                column: "AssignedStaffId",
                principalTable: "user_account",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_order_header_user_account_AssignedStaffId",
                table: "order_header");

            migrationBuilder.DropIndex(
                name: "IX_order_header_AssignedStaffId",
                table: "order_header");

            migrationBuilder.DropColumn(
                name: "AssignedStaffId",
                table: "order_header");
        }
    }
}
