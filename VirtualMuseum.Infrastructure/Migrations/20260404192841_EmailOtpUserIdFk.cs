using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtualMuseum.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EmailOtpUserIdFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "EmailOtps",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "EmailOtps" AS eo
                SET "UserId" = u."Id"
                FROM "Users" AS u
                WHERE u."Email" = eo."Email";
                """);

            migrationBuilder.Sql("""DELETE FROM "EmailOtps" WHERE "UserId" IS NULL""");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "EmailOtps",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Email",
                table: "EmailOtps");

            migrationBuilder.CreateIndex(
                name: "IX_EmailOtps_UserId",
                table: "EmailOtps",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_EmailOtps_Users_UserId",
                table: "EmailOtps",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmailOtps_Users_UserId",
                table: "EmailOtps");

            migrationBuilder.DropIndex(
                name: "IX_EmailOtps_UserId",
                table: "EmailOtps");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "EmailOtps",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "EmailOtps" AS eo
                SET "Email" = u."Email"
                FROM "Users" AS u
                WHERE u."Id" = eo."UserId";
                """);

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "EmailOtps");
        }
    }
}
