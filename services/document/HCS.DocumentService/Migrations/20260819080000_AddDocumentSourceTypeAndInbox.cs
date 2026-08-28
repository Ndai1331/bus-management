using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.DocumentService.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(DocumentServiceDbContext))]
    [Migration("20260819080000_AddDocumentSourceTypeAndInbox")]
    public partial class AddDocumentSourceTypeAndInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceType",
                schema: "document",
                table: "Documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentDocumentId",
                schema: "document",
                table: "Documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FromUserId",
                schema: "document",
                table: "Documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationUnitId",
                schema: "document",
                table: "Documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCurrent",
                schema: "document",
                table: "DocumentAssignments",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "StepCode",
                schema: "document",
                table: "DocumentAssignments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_SourceType",
                schema: "document",
                table: "Documents",
                column: "SourceType");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ParentDocumentId",
                schema: "document",
                table: "Documents",
                column: "ParentDocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_SourceType",
                schema: "document",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_ParentDocumentId",
                schema: "document",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SourceType",
                schema: "document",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ParentDocumentId",
                schema: "document",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "FromUserId",
                schema: "document",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "OrganizationUnitId",
                schema: "document",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IsCurrent",
                schema: "document",
                table: "DocumentAssignments");

            migrationBuilder.DropColumn(
                name: "StepCode",
                schema: "document",
                table: "DocumentAssignments");
        }
    }
}
