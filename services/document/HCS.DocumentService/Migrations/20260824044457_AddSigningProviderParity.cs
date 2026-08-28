using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.DocumentService.Migrations
{
    /// <inheritdoc />
    public partial class AddSigningProviderParity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "document",
                table: "UserSignatures",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ProtectedSecret",
                schema: "document",
                table: "UserSignatures",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderCode",
                schema: "document",
                table: "UserSignatures",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SealImageBase64",
                schema: "document",
                table: "UserSignatures",
                type: "character varying(4000000)",
                maxLength: 4000000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TokenRef",
                schema: "document",
                table: "UserSignatures",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidFrom",
                schema: "document",
                table: "UserSignatures",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidTo",
                schema: "document",
                table: "UserSignatures",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowDigitalSign",
                schema: "document",
                table: "SigningCredentials",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowElectronicSign",
                schema: "document",
                table: "SigningCredentials",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "ApiTimeoutSeconds",
                schema: "document",
                table: "SigningCredentials",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<string>(
                name: "LayoutImageBase64",
                schema: "document",
                table: "SigningCredentials",
                type: "character varying(4000000)",
                maxLength: 4000000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderCode",
                schema: "document",
                table: "SigningCredentials",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "RequireOtp",
                schema: "document",
                table: "SigningCredentials",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SignHeight",
                schema: "document",
                table: "SigningCredentials",
                type: "integer",
                nullable: false,
                defaultValue: 70);

            migrationBuilder.AddColumn<int>(
                name: "SignWidth",
                schema: "document",
                table: "SigningCredentials",
                type: "integer",
                nullable: false,
                defaultValue: 150);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "document",
                table: "UserSignatures");

            migrationBuilder.DropColumn(
                name: "ProtectedSecret",
                schema: "document",
                table: "UserSignatures");

            migrationBuilder.DropColumn(
                name: "ProviderCode",
                schema: "document",
                table: "UserSignatures");

            migrationBuilder.DropColumn(
                name: "SealImageBase64",
                schema: "document",
                table: "UserSignatures");

            migrationBuilder.DropColumn(
                name: "TokenRef",
                schema: "document",
                table: "UserSignatures");

            migrationBuilder.DropColumn(
                name: "ValidFrom",
                schema: "document",
                table: "UserSignatures");

            migrationBuilder.DropColumn(
                name: "ValidTo",
                schema: "document",
                table: "UserSignatures");

            migrationBuilder.DropColumn(
                name: "AllowDigitalSign",
                schema: "document",
                table: "SigningCredentials");

            migrationBuilder.DropColumn(
                name: "AllowElectronicSign",
                schema: "document",
                table: "SigningCredentials");

            migrationBuilder.DropColumn(
                name: "ApiTimeoutSeconds",
                schema: "document",
                table: "SigningCredentials");

            migrationBuilder.DropColumn(
                name: "LayoutImageBase64",
                schema: "document",
                table: "SigningCredentials");

            migrationBuilder.DropColumn(
                name: "ProviderCode",
                schema: "document",
                table: "SigningCredentials");

            migrationBuilder.DropColumn(
                name: "RequireOtp",
                schema: "document",
                table: "SigningCredentials");

            migrationBuilder.DropColumn(
                name: "SignHeight",
                schema: "document",
                table: "SigningCredentials");

            migrationBuilder.DropColumn(
                name: "SignWidth",
                schema: "document",
                table: "SigningCredentials");
        }
    }
}
