using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.DocumentService.Migrations
{
    /// <inheritdoc />
    public partial class HardenDocumentOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_PublishedAt_CreationTime",
                schema: "document",
                table: "OutboxMessages");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeadLetteredAt",
                schema: "document",
                table: "OutboxMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeaseId",
                schema: "document",
                table: "OutboxMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseUntil",
                schema: "document",
                table: "OutboxMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                schema: "document",
                table: "OutboxMessages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_PublishedAt_DeadLetteredAt_LeaseUntil_NextAt~",
                schema: "document",
                table: "OutboxMessages",
                columns: new[] { "PublishedAt", "DeadLetteredAt", "LeaseUntil", "NextAttemptAt", "CreationTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_PublishedAt_DeadLetteredAt_LeaseUntil_NextAt~",
                schema: "document",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "DeadLetteredAt",
                schema: "document",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "LeaseId",
                schema: "document",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "LeaseUntil",
                schema: "document",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                schema: "document",
                table: "OutboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_PublishedAt_CreationTime",
                schema: "document",
                table: "OutboxMessages",
                columns: new[] { "PublishedAt", "CreationTime" });
        }
    }
}
