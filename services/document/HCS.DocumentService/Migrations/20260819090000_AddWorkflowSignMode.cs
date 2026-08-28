using System;
using HCS.DocumentService;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.DocumentService.Migrations
{
    [DbContext(typeof(DocumentServiceDbContext))]
    [Migration("20260819090000_AddWorkflowSignMode")]
    public partial class AddWorkflowSignMode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SignMode",
                schema: "document",
                table: "WorkflowDefinitions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "SEQUENTIAL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SignMode",
                schema: "document",
                table: "WorkflowDefinitions");
        }
    }
}
