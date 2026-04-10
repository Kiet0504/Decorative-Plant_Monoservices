using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace decorativeplant_be.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyIdToUserAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "user_account",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_account_CompanyId",
                table: "user_account",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_user_account_company_CompanyId",
                table: "user_account",
                column: "CompanyId",
                principalTable: "company",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_account_company_CompanyId",
                table: "user_account");

            migrationBuilder.DropIndex(
                name: "IX_user_account_CompanyId",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "user_account");
        }
    }
}
