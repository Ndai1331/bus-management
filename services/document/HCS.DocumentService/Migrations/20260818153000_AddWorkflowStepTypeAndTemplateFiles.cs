using System;
using HCS.DocumentService;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.DocumentService.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(DocumentServiceDbContext))]
    [Migration("20260818153000_AddWorkflowStepTypeAndTemplateFiles")]
    public partial class AddWorkflowStepTypeAndTemplateFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssigneeUserId",
                schema: "document",
                table: "WorkflowSteps",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                schema: "document",
                table: "WorkflowSteps",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "PROCESS");

            migrationBuilder.AddColumn<Guid>(
                name: "AssigneeUserId",
                schema: "document",
                table: "ApprovalTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PdfFileId",
                schema: "document",
                table: "WorkflowTemplates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfFileName",
                schema: "document",
                table: "WorkflowTemplates",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfContentType",
                schema: "document",
                table: "WorkflowTemplates",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfBlobName",
                schema: "document",
                table: "WorkflowTemplates",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WordFileId",
                schema: "document",
                table: "WorkflowTemplates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WordFileName",
                schema: "document",
                table: "WorkflowTemplates",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WordContentType",
                schema: "document",
                table: "WorkflowTemplates",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WordBlobName",
                schema: "document",
                table: "WorkflowTemplates",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AssigneeUserId", schema: "document", table: "WorkflowSteps");
            migrationBuilder.DropColumn(name: "Type", schema: "document", table: "WorkflowSteps");
            migrationBuilder.DropColumn(name: "AssigneeUserId", schema: "document", table: "ApprovalTasks");
            migrationBuilder.DropColumn(name: "PdfFileId", schema: "document", table: "WorkflowTemplates");
            migrationBuilder.DropColumn(name: "PdfFileName", schema: "document", table: "WorkflowTemplates");
            migrationBuilder.DropColumn(name: "PdfContentType", schema: "document", table: "WorkflowTemplates");
            migrationBuilder.DropColumn(name: "PdfBlobName", schema: "document", table: "WorkflowTemplates");
            migrationBuilder.DropColumn(name: "WordFileId", schema: "document", table: "WorkflowTemplates");
            migrationBuilder.DropColumn(name: "WordFileName", schema: "document", table: "WorkflowTemplates");
            migrationBuilder.DropColumn(name: "WordContentType", schema: "document", table: "WorkflowTemplates");
            migrationBuilder.DropColumn(name: "WordBlobName", schema: "document", table: "WorkflowTemplates");
        }
    }
}
