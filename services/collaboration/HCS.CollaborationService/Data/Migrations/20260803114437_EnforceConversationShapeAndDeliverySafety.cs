using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.CollaborationService.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceConversationShapeAndDeliverySafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CollaborationWorkSubjects_SubjectType_ProjectId_TaskId",
                table: "CollaborationWorkSubjects");

            migrationBuilder.DropIndex(
                name: "IX_CollaborationPushDeliveries_DeliveredAt_LeaseUntil_NextAtte~",
                table: "CollaborationPushDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_CollaborationConversations_ProjectId",
                table: "CollaborationConversations");

            migrationBuilder.DropIndex(
                name: "IX_CollaborationConversations_TaskId",
                table: "CollaborationConversations");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastOccurredAtUtc",
                table: "CollaborationWorkSubjects",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeadLetteredAt",
                table: "CollaborationPushDeliveries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "CollaborationPushDeliveries",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationWorkSubjects_ProjectId",
                table: "CollaborationWorkSubjects",
                column: "ProjectId",
                unique: true,
                filter: "\"SubjectType\" = 'Project' AND \"TaskId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationWorkSubjects_TaskId",
                table: "CollaborationWorkSubjects",
                column: "TaskId",
                unique: true,
                filter: "\"SubjectType\" = 'Task' AND \"TaskId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationPushDeliveries_DeliveredAt_DeadLetteredAt_Leas~",
                table: "CollaborationPushDeliveries",
                columns: new[] { "DeliveredAt", "DeadLetteredAt", "LeaseUntil", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationConversations_ProjectId",
                table: "CollaborationConversations",
                column: "ProjectId",
                unique: true,
                filter: "\"Type\" = 2 AND \"ProjectId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationConversations_TaskId",
                table: "CollaborationConversations",
                column: "TaskId",
                unique: true,
                filter: "\"Type\" = 3 AND \"TaskId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversation_SubjectShape",
                table: "CollaborationConversations",
                sql: "(\"Type\" IN (0,1) AND \"ProjectId\" IS NULL AND \"TaskId\" IS NULL) OR (\"Type\" = 2 AND \"ProjectId\" IS NOT NULL AND \"TaskId\" IS NULL) OR (\"Type\" = 3 AND \"ProjectId\" IS NOT NULL AND \"TaskId\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CollaborationWorkSubjects_ProjectId",
                table: "CollaborationWorkSubjects");

            migrationBuilder.DropIndex(
                name: "IX_CollaborationWorkSubjects_TaskId",
                table: "CollaborationWorkSubjects");

            migrationBuilder.DropIndex(
                name: "IX_CollaborationPushDeliveries_DeliveredAt_DeadLetteredAt_Leas~",
                table: "CollaborationPushDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_CollaborationConversations_ProjectId",
                table: "CollaborationConversations");

            migrationBuilder.DropIndex(
                name: "IX_CollaborationConversations_TaskId",
                table: "CollaborationConversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversation_SubjectShape",
                table: "CollaborationConversations");

            migrationBuilder.DropColumn(
                name: "LastOccurredAtUtc",
                table: "CollaborationWorkSubjects");

            migrationBuilder.DropColumn(
                name: "DeadLetteredAt",
                table: "CollaborationPushDeliveries");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "CollaborationPushDeliveries");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationWorkSubjects_SubjectType_ProjectId_TaskId",
                table: "CollaborationWorkSubjects",
                columns: new[] { "SubjectType", "ProjectId", "TaskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationPushDeliveries_DeliveredAt_LeaseUntil_NextAtte~",
                table: "CollaborationPushDeliveries",
                columns: new[] { "DeliveredAt", "LeaseUntil", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationConversations_ProjectId",
                table: "CollaborationConversations",
                column: "ProjectId",
                unique: true,
                filter: "\"ProjectId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationConversations_TaskId",
                table: "CollaborationConversations",
                column: "TaskId",
                unique: true,
                filter: "\"TaskId\" IS NOT NULL");
        }
    }
}
