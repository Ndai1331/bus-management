using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.WorkManagementService.Migrations
{
    /// <inheritdoc />
    public partial class AddSurveyPublicCollection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviceType",
                schema: "hcs_work",
                table: "SurveySessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                schema: "hcs_work",
                table: "SurveySessions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                schema: "hcs_work",
                table: "SurveySessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                schema: "hcs_work",
                table: "SurveySessions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientCode",
                schema: "hcs_work",
                table: "SurveySessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                schema: "hcs_work",
                table: "SurveySessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionDisplay",
                schema: "hcs_work",
                table: "SurveySessions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SurveyTime",
                schema: "hcs_work",
                table: "SurveySessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "hcs_work",
                table: "SurveyLocations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Image",
                schema: "hcs_work",
                table: "SurveyCriteria",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LocationId",
                schema: "hcs_work",
                table: "SurveyCriteria",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SurveySessions_IsPublic",
                schema: "hcs_work",
                table: "SurveySessions",
                column: "IsPublic");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyCriteria_LocationId",
                schema: "hcs_work",
                table: "SurveyCriteria",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_SurveyCriteria_SurveyLocations_LocationId",
                schema: "hcs_work",
                table: "SurveyCriteria",
                column: "LocationId",
                principalSchema: "hcs_work",
                principalTable: "SurveyLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SurveyCriteria_SurveyLocations_LocationId",
                schema: "hcs_work",
                table: "SurveyCriteria");

            migrationBuilder.DropIndex(
                name: "IX_SurveySessions_IsPublic",
                schema: "hcs_work",
                table: "SurveySessions");

            migrationBuilder.DropIndex(
                name: "IX_SurveyCriteria_LocationId",
                schema: "hcs_work",
                table: "SurveyCriteria");

            migrationBuilder.DropColumn(
                name: "DeviceType",
                schema: "hcs_work",
                table: "SurveySessions");

            migrationBuilder.DropColumn(
                name: "FullName",
                schema: "hcs_work",
                table: "SurveySessions");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                schema: "hcs_work",
                table: "SurveySessions");

            migrationBuilder.DropColumn(
                name: "Note",
                schema: "hcs_work",
                table: "SurveySessions");

            migrationBuilder.DropColumn(
                name: "PatientCode",
                schema: "hcs_work",
                table: "SurveySessions");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                schema: "hcs_work",
                table: "SurveySessions");

            migrationBuilder.DropColumn(
                name: "SessionDisplay",
                schema: "hcs_work",
                table: "SurveySessions");

            migrationBuilder.DropColumn(
                name: "SurveyTime",
                schema: "hcs_work",
                table: "SurveySessions");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "hcs_work",
                table: "SurveyLocations");

            migrationBuilder.DropColumn(
                name: "Image",
                schema: "hcs_work",
                table: "SurveyCriteria");

            migrationBuilder.DropColumn(
                name: "LocationId",
                schema: "hcs_work",
                table: "SurveyCriteria");
        }
    }
}
