using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.CollaborationService.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardenCollaborationDurability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CollaborationPushDeliveries_DeliveredAt_NextAttemptAt",
                table: "CollaborationPushDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_CollaborationOutbox_PublishedAt_OccurredAt",
                table: "CollaborationOutbox");

            migrationBuilder.AddColumn<Guid>(
                name: "LeaseId",
                table: "CollaborationPushDeliveries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseUntil",
                table: "CollaborationPushDeliveries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeadLetteredAt",
                table: "CollaborationOutbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "CollaborationOutbox",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeaseId",
                table: "CollaborationOutbox",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseUntil",
                table: "CollaborationOutbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CollaborationWorkSubjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationWorkSubjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CollaborationWorkSubjectMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationWorkSubjectMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollaborationWorkSubjectMembers_CollaborationWorkSubjects_S~",
                        column: x => x.SubjectId,
                        principalTable: "CollaborationWorkSubjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationPushDeliveries_DeliveredAt_LeaseUntil_NextAtte~",
                table: "CollaborationPushDeliveries",
                columns: new[] { "DeliveredAt", "LeaseUntil", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationOutbox_PublishedAt_DeadLetteredAt_LeaseUntil_O~",
                table: "CollaborationOutbox",
                columns: new[] { "PublishedAt", "DeadLetteredAt", "LeaseUntil", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationWorkSubjectMembers_SubjectId_UserId",
                table: "CollaborationWorkSubjectMembers",
                columns: new[] { "SubjectId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationWorkSubjectMembers_UserId",
                table: "CollaborationWorkSubjectMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationWorkSubjects_SubjectType_ProjectId_TaskId",
                table: "CollaborationWorkSubjects",
                columns: new[] { "SubjectType", "ProjectId", "TaskId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollaborationWorkSubjectMembers");

            migrationBuilder.DropTable(
                name: "CollaborationWorkSubjects");

            migrationBuilder.DropIndex(
                name: "IX_CollaborationPushDeliveries_DeliveredAt_LeaseUntil_NextAtte~",
                table: "CollaborationPushDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_CollaborationOutbox_PublishedAt_DeadLetteredAt_LeaseUntil_O~",
                table: "CollaborationOutbox");

            migrationBuilder.DropColumn(
                name: "LeaseId",
                table: "CollaborationPushDeliveries");

            migrationBuilder.DropColumn(
                name: "LeaseUntil",
                table: "CollaborationPushDeliveries");

            migrationBuilder.DropColumn(
                name: "DeadLetteredAt",
                table: "CollaborationOutbox");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "CollaborationOutbox");

            migrationBuilder.DropColumn(
                name: "LeaseId",
                table: "CollaborationOutbox");

            migrationBuilder.DropColumn(
                name: "LeaseUntil",
                table: "CollaborationOutbox");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationPushDeliveries_DeliveredAt_NextAttemptAt",
                table: "CollaborationPushDeliveries",
                columns: new[] { "DeliveredAt", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationOutbox_PublishedAt_OccurredAt",
                table: "CollaborationOutbox",
                columns: new[] { "PublishedAt", "OccurredAt" });
        }
    }
}
