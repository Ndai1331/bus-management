using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.WorkManagementService.Migrations
{
    /// <inheritdoc />
    public partial class LeaseWorkOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_PublishedAt_DeadLetteredAt_CreationTime",
                schema: "hcs_work",
                table: "OutboxMessages");

            migrationBuilder.AddColumn<Guid>(
                name: "LeaseId",
                schema: "hcs_work",
                table: "OutboxMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseUntil",
                schema: "hcs_work",
                table: "OutboxMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_PublishedAt_DeadLetteredAt_LeaseUntil_Creati~",
                schema: "hcs_work",
                table: "OutboxMessages",
                columns: new[] { "PublishedAt", "DeadLetteredAt", "LeaseUntil", "CreationTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_PublishedAt_DeadLetteredAt_LeaseUntil_Creati~",
                schema: "hcs_work",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "LeaseId",
                schema: "hcs_work",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "LeaseUntil",
                schema: "hcs_work",
                table: "OutboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_PublishedAt_DeadLetteredAt_CreationTime",
                schema: "hcs_work",
                table: "OutboxMessages",
                columns: new[] { "PublishedAt", "DeadLetteredAt", "CreationTime" });
        }
    }
}
