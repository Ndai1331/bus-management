using System;
using HCS.DocumentService;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.DocumentService.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(DocumentServiceDbContext))]
    [Migration("20260819120000_AddWorkflowKindsAndStepAssignments")]
    public partial class AddWorkflowKindsAndStepAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowKinds",
                schema: "document",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_WorkflowKinds", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowKinds_Code",
                schema: "document",
                table: "WorkflowKinds",
                column: "Code",
                unique: true);

            migrationBuilder.AddColumn<Guid>(
                name: "KindId",
                schema: "document",
                table: "WorkflowDefinitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "document",
                table: "WorkflowDefinitions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "document",
                table: "WorkflowDefinitions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_KindId",
                schema: "document",
                table: "WorkflowDefinitions",
                column: "KindId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowDefinitions_WorkflowKinds_KindId",
                schema: "document",
                table: "WorkflowDefinitions",
                column: "KindId",
                principalSchema: "document",
                principalTable: "WorkflowKinds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddColumn<string>(
                name: "AssigneeType",
                schema: "document",
                table: "WorkflowSteps",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "SpecificUser");

            migrationBuilder.AddColumn<Guid>(
                name: "RoleId",
                schema: "document",
                table: "WorkflowSteps",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserIdsJson",
                schema: "document",
                table: "WorkflowSteps",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "DepartmentIdsJson",
                schema: "document",
                table: "WorkflowSteps",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "SlaDays",
                schema: "document",
                table: "WorkflowSteps",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowReturn",
                schema: "document",
                table: "WorkflowSteps",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueAt",
                schema: "document",
                table: "ApprovalTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ViewScopesJson",
                schema: "document",
                table: "WorkflowInstances",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowDefinitions_WorkflowKinds_KindId",
                schema: "document",
                table: "WorkflowDefinitions");
            migrationBuilder.DropIndex(name: "IX_WorkflowDefinitions_KindId", schema: "document", table: "WorkflowDefinitions");
            migrationBuilder.DropColumn(name: "KindId", schema: "document", table: "WorkflowDefinitions");
            migrationBuilder.DropColumn(name: "Description", schema: "document", table: "WorkflowDefinitions");
            migrationBuilder.DropColumn(name: "IsActive", schema: "document", table: "WorkflowDefinitions");
            migrationBuilder.DropColumn(name: "AssigneeType", schema: "document", table: "WorkflowSteps");
            migrationBuilder.DropColumn(name: "RoleId", schema: "document", table: "WorkflowSteps");
            migrationBuilder.DropColumn(name: "UserIdsJson", schema: "document", table: "WorkflowSteps");
            migrationBuilder.DropColumn(name: "DepartmentIdsJson", schema: "document", table: "WorkflowSteps");
            migrationBuilder.DropColumn(name: "SlaDays", schema: "document", table: "WorkflowSteps");
            migrationBuilder.DropColumn(name: "AllowReturn", schema: "document", table: "WorkflowSteps");
            migrationBuilder.DropColumn(name: "DueAt", schema: "document", table: "ApprovalTasks");
            migrationBuilder.DropColumn(name: "ViewScopesJson", schema: "document", table: "WorkflowInstances");
            migrationBuilder.DropTable(name: "WorkflowKinds", schema: "document");
        }
    }
}
