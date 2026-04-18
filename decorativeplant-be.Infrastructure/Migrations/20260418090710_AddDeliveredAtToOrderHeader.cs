using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace decorativeplant_be.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveredAtToOrderHeader : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "order_header",
                type: "timestamp with time zone",
                nullable: true);

            // Backfill DeliveredAt from Notes->status_history for any order already
            // in "delivered" state so AutoCompleteDeliveredOrdersJob (which now queries
            // the scalar column) doesn't skip rows that pre-date this column.
            migrationBuilder.Sql(@"
                UPDATE order_header oh
                SET ""DeliveredAt"" = sub.delivered_at
                FROM (
                    SELECT
                        h_order.""Id"" AS order_id,
                        MAX((h.elem->>'at')::timestamptz) AS delivered_at
                    FROM order_header h_order,
                         LATERAL jsonb_array_elements(h_order.""Notes""->'status_history') AS h(elem)
                    WHERE h_order.""Status"" = 'delivered'
                      AND h_order.""Notes"" ? 'status_history'
                      AND h.elem->>'to' = 'delivered'
                      AND h.elem->>'at' IS NOT NULL
                    GROUP BY h_order.""Id""
                ) sub
                WHERE oh.""Id"" = sub.order_id
                  AND oh.""DeliveredAt"" IS NULL;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_order_header_DeliveredAt_delivered",
                table: "order_header",
                column: "DeliveredAt",
                filter: "\"Status\" = 'delivered'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_order_header_DeliveredAt_delivered",
                table: "order_header");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "order_header");
        }
    }
}
