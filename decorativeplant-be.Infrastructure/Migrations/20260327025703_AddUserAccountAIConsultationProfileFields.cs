using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace decorativeplant_be.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAccountAIConsultationProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BudgetRange",
                table: "user_account",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasChildrenOrPets",
                table: "user_account",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HumidityLevel",
                table: "user_account",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsProfileCompleted",
                table: "user_account",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PlacementLocation",
                table: "user_account",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "PlantGoals",
                table: "user_account",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredStyle",
                table: "user_account",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoomTemperatureRange",
                table: "user_account",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpaceSize",
                table: "user_account",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SunlightExposure",
                table: "user_account",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WateringFrequency",
                table: "user_account",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BudgetRange",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "HasChildrenOrPets",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "HumidityLevel",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "IsProfileCompleted",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "PlacementLocation",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "PlantGoals",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "PreferredStyle",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "RoomTemperatureRange",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "SpaceSize",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "SunlightExposure",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "WateringFrequency",
                table: "user_account");
        }
    }
}
