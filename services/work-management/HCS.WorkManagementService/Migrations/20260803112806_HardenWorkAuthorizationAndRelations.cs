using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.WorkManagementService.Migrations
{
    /// <inheritdoc />
    public partial class HardenWorkAuthorizationAndRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_PublishedAt_CreationTime",
                schema: "hcs_work",
                table: "OutboxMessages");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                schema: "hcs_work",
                table: "SurveySessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UploadedByUserId",
                schema: "hcs_work",
                table: "SurveyFiles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                schema: "hcs_work",
                table: "Projects",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeadLetteredAt",
                schema: "hcs_work",
                table: "OutboxMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                schema: "hcs_work",
                table: "CalendarEvents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_SurveySessions_LocationId",
                schema: "hcs_work",
                table: "SurveySessions",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_SurveySessions_OwnerUserId",
                schema: "hcs_work",
                table: "SurveySessions",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyResults_CriteriaId",
                schema: "hcs_work",
                table: "SurveyResults",
                column: "CriteriaId");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyFiles_UploadedByUserId",
                schema: "hcs_work",
                table: "SurveyFiles",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OwnerUserId",
                schema: "hcs_work",
                table: "Projects",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_PublishedAt_DeadLetteredAt_CreationTime",
                schema: "hcs_work",
                table: "OutboxMessages",
                columns: new[] { "PublishedAt", "DeadLetteredAt", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_OwnerUserId",
                schema: "hcs_work",
                table: "CalendarEvents",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarEventParticipants_CalendarEvents_CalendarEventId",
                schema: "hcs_work",
                table: "CalendarEventParticipants",
                column: "CalendarEventId",
                principalSchema: "hcs_work",
                principalTable: "CalendarEvents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectMembers_Projects_ProjectId",
                schema: "hcs_work",
                table: "ProjectMembers",
                column: "ProjectId",
                principalSchema: "hcs_work",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectTaskAssignments_ProjectTasks_ProjectTaskId",
                schema: "hcs_work",
                table: "ProjectTaskAssignments",
                column: "ProjectTaskId",
                principalSchema: "hcs_work",
                principalTable: "ProjectTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectTaskDocuments_ProjectTasks_ProjectTaskId",
                schema: "hcs_work",
                table: "ProjectTaskDocuments",
                column: "ProjectTaskId",
                principalSchema: "hcs_work",
                principalTable: "ProjectTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectTasks_ProjectTasks_ParentTaskId",
                schema: "hcs_work",
                table: "ProjectTasks",
                column: "ParentTaskId",
                principalSchema: "hcs_work",
                principalTable: "ProjectTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectTasks_Projects_ProjectId",
                schema: "hcs_work",
                table: "ProjectTasks",
                column: "ProjectId",
                principalSchema: "hcs_work",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SurveyFiles_SurveySessions_SessionId",
                schema: "hcs_work",
                table: "SurveyFiles",
                column: "SessionId",
                principalSchema: "hcs_work",
                principalTable: "SurveySessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SurveyResults_SurveyCriteria_CriteriaId",
                schema: "hcs_work",
                table: "SurveyResults",
                column: "CriteriaId",
                principalSchema: "hcs_work",
                principalTable: "SurveyCriteria",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SurveyResults_SurveySessions_SessionId",
                schema: "hcs_work",
                table: "SurveyResults",
                column: "SessionId",
                principalSchema: "hcs_work",
                principalTable: "SurveySessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SurveySessions_SurveyLocations_LocationId",
                schema: "hcs_work",
                table: "SurveySessions",
                column: "LocationId",
                principalSchema: "hcs_work",
                principalTable: "SurveyLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CalendarEventParticipants_CalendarEvents_CalendarEventId",
                schema: "hcs_work",
                table: "CalendarEventParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectMembers_Projects_ProjectId",
                schema: "hcs_work",
                table: "ProjectMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectTaskAssignments_ProjectTasks_ProjectTaskId",
                schema: "hcs_work",
                table: "ProjectTaskAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectTaskDocuments_ProjectTasks_ProjectTaskId",
                schema: "hcs_work",
                table: "ProjectTaskDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectTasks_ProjectTasks_ParentTaskId",
                schema: "hcs_work",
                table: "ProjectTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectTasks_Projects_ProjectId",
                schema: "hcs_work",
                table: "ProjectTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_SurveyFiles_SurveySessions_SessionId",
                schema: "hcs_work",
                table: "SurveyFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_SurveyResults_SurveyCriteria_CriteriaId",
                schema: "hcs_work",
                table: "SurveyResults");

            migrationBuilder.DropForeignKey(
                name: "FK_SurveyResults_SurveySessions_SessionId",
                schema: "hcs_work",
                table: "SurveyResults");

            migrationBuilder.DropForeignKey(
                name: "FK_SurveySessions_SurveyLocations_LocationId",
                schema: "hcs_work",
                table: "SurveySessions");

            migrationBuilder.DropIndex(
                name: "IX_SurveySessions_LocationId",
                schema: "hcs_work",
                table: "SurveySessions");

            migrationBuilder.DropIndex(
                name: "IX_SurveySessions_OwnerUserId",
                schema: "hcs_work",
                table: "SurveySessions");

            migrationBuilder.DropIndex(
                name: "IX_SurveyResults_CriteriaId",
                schema: "hcs_work",
                table: "SurveyResults");

            migrationBuilder.DropIndex(
                name: "IX_SurveyFiles_UploadedByUserId",
                schema: "hcs_work",
                table: "SurveyFiles");

            migrationBuilder.DropIndex(
                name: "IX_Projects_OwnerUserId",
                schema: "hcs_work",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_PublishedAt_DeadLetteredAt_CreationTime",
                schema: "hcs_work",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_OwnerUserId",
                schema: "hcs_work",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                schema: "hcs_work",
                table: "SurveySessions");

            migrationBuilder.DropColumn(
                name: "UploadedByUserId",
                schema: "hcs_work",
                table: "SurveyFiles");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                schema: "hcs_work",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DeadLetteredAt",
                schema: "hcs_work",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                schema: "hcs_work",
                table: "CalendarEvents");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_PublishedAt_CreationTime",
                schema: "hcs_work",
                table: "OutboxMessages",
                columns: new[] { "PublishedAt", "CreationTime" });
        }
    }
}
