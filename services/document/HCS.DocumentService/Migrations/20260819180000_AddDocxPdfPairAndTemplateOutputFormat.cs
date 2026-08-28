using System;
using HCS.DocumentService;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.DocumentService.Migrations
{
    [DbContext(typeof(DocumentServiceDbContext))]
    [Migration("20260819180000_AddDocxPdfPairAndTemplateOutputFormat")]
    public partial class AddDocxPdfPairAndTemplateOutputFormat : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PairedFileId",
                schema: "document",
                table: "DocumentFiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutputFormat",
                schema: "document",
                table: "WorkflowTemplates",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "PDF");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PairedFileId", schema: "document", table: "DocumentFiles");
            migrationBuilder.DropColumn(name: "OutputFormat", schema: "document", table: "WorkflowTemplates");
        }
    }
}
