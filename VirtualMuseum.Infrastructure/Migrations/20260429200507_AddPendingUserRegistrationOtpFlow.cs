using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtualMuseum.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingUserRegistrationOtpFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingUserRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Region = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    OtpCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OtpExpirationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingUserRegistrations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingUserRegistrations_Email",
                table: "PendingUserRegistrations",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingUserRegistrations");
        }
    }
}
