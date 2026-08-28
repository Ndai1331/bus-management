using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.DocumentService.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(DocumentServiceDbContext))]
    [Migration("20260818120000_AddDocumentClassificationAndUserSignatures")]
    public partial class AddDocumentClassificationAndUserSignatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConfidentialityId",
                schema: "document",
                table: "Documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DocumentTypeId",
                schema: "document",
                table: "Documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SectorId",
                schema: "document",
                table: "Documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UrgencyId",
                schema: "document",
                table: "Documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserSignatures",
                schema: "document",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BlobName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSignatures", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSignatures_UserId",
                schema: "document",
                table: "UserSignatures",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSignatures",
                schema: "document");

            migrationBuilder.DropColumn(
                name: "ConfidentialityId",
                schema: "document",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DocumentTypeId",
                schema: "document",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SectorId",
                schema: "document",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "UrgencyId",
                schema: "document",
                table: "Documents");
        }
    }
}
