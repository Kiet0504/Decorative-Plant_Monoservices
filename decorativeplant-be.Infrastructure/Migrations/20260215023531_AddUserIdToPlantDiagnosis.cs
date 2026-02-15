using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace decorativeplant_be.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToPlantDiagnosis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "plant_diagnosis",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_plant_diagnosis_UserId",
                table: "plant_diagnosis",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_plant_diagnosis_user_account_UserId",
                table: "plant_diagnosis",
                column: "UserId",
                principalTable: "user_account",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_plant_diagnosis_user_account_UserId",
                table: "plant_diagnosis");

            migrationBuilder.DropIndex(
                name: "IX_plant_diagnosis_UserId",
                table: "plant_diagnosis");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "plant_diagnosis");
        }
    }
}
