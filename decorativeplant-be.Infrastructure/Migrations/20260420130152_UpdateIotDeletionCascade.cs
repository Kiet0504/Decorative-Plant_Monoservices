using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace decorativeplant_be.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateIotDeletionCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_automation_rule_iot_device_DeviceId",
                table: "automation_rule");

            migrationBuilder.DropForeignKey(
                name: "FK_IotAlerts_iot_device_DeviceId",
                table: "IotAlerts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_IotAlerts",
                table: "IotAlerts");

            migrationBuilder.RenameTable(
                name: "IotAlerts",
                newName: "iot_alert");

            migrationBuilder.RenameIndex(
                name: "IX_IotAlerts_DeviceId",
                table: "iot_alert",
                newName: "IX_iot_alert_DeviceId");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "iot_alert",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddPrimaryKey(
                name: "PK_iot_alert",
                table: "iot_alert",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_automation_rule_iot_device_DeviceId",
                table: "automation_rule",
                column: "DeviceId",
                principalTable: "iot_device",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_iot_alert_iot_device_DeviceId",
                table: "iot_alert",
                column: "DeviceId",
                principalTable: "iot_device",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_automation_rule_iot_device_DeviceId",
                table: "automation_rule");

            migrationBuilder.DropForeignKey(
                name: "FK_iot_alert_iot_device_DeviceId",
                table: "iot_alert");

            migrationBuilder.DropPrimaryKey(
                name: "PK_iot_alert",
                table: "iot_alert");

            migrationBuilder.RenameTable(
                name: "iot_alert",
                newName: "IotAlerts");

            migrationBuilder.RenameIndex(
                name: "IX_iot_alert_DeviceId",
                table: "IotAlerts",
                newName: "IX_IotAlerts_DeviceId");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "IotAlerts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddPrimaryKey(
                name: "PK_IotAlerts",
                table: "IotAlerts",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_automation_rule_iot_device_DeviceId",
                table: "automation_rule",
                column: "DeviceId",
                principalTable: "iot_device",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_IotAlerts_iot_device_DeviceId",
                table: "IotAlerts",
                column: "DeviceId",
                principalTable: "iot_device",
                principalColumn: "Id");
        }
    }
}
