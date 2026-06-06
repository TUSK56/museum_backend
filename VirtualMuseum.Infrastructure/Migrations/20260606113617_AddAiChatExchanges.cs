using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtualMuseum.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiChatExchanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiChatExchanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UserDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SessionKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UserMessage = table.Column<string>(type: "text", nullable: false),
                    AssistantReply = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FromN8n = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiChatExchanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiChatExchanges_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiChatExchanges_CreatedAt",
                table: "AiChatExchanges",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AiChatExchanges_UserId",
                table: "AiChatExchanges",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiChatExchanges");
        }
    }
}
