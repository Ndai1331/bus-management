using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.DocumentService.Migrations
{
    /// <inheritdoc />
    public partial class ScopeSigningIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SigningAttempts_IdempotencyKey",
                schema: "document",
                table: "SigningAttempts");

            migrationBuilder.CreateIndex(
                name: "IX_SigningAttempts_UserId_DocumentId_FileId_Kind_IdempotencyKey",
                schema: "document",
                table: "SigningAttempts",
                columns: new[] { "UserId", "DocumentId", "FileId", "Kind", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SigningAttempts_UserId_DocumentId_FileId_Kind_IdempotencyKey",
                schema: "document",
                table: "SigningAttempts");

            migrationBuilder.CreateIndex(
                name: "IX_SigningAttempts_IdempotencyKey",
                schema: "document",
                table: "SigningAttempts",
                column: "IdempotencyKey",
                unique: true);
        }
    }
}
