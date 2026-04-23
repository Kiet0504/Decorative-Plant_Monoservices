using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace decorativeplant_be.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiChatHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_chat_thread",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_chat_thread", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_chat_thread_user_account_UserId",
                        column: x => x.UserId,
                        principalTable: "user_account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_chat_message",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    AttachmentUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AttachmentMimeType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DiagnosisJson = table.Column<string>(type: "jsonb", nullable: true),
                    RecommendationsJson = table.Column<string>(type: "jsonb", nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_chat_message", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_chat_message_ai_chat_thread_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "ai_chat_thread",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_chat_message_ThreadId_CreatedAt",
                table: "ai_chat_message",
                columns: new[] { "ThreadId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_chat_thread_UserId",
                table: "ai_chat_thread",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_chat_message");

            migrationBuilder.DropTable(
                name: "ai_chat_thread");
        }
    }
}
