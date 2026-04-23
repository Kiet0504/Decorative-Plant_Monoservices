using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace decorativeplant_be.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnableMultiAiChatThreads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ai_chat_thread_UserId",
                table: "ai_chat_thread");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "ai_chat_thread",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_chat_thread_UserId",
                table: "ai_chat_thread",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ai_chat_thread_UserId",
                table: "ai_chat_thread");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "ai_chat_thread");

            migrationBuilder.CreateIndex(
                name: "IX_ai_chat_thread_UserId",
                table: "ai_chat_thread",
                column: "UserId",
                unique: true);
        }
    }
}
