using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.BusManagementService.Migrations
{
    /// <inheritdoc />
    public partial class AddBusPhaseTwo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VehicleLegalDocuments_ExpiresOn_IsActive",
                schema: "hcs_bus_management",
                table: "VehicleLegalDocuments");

            migrationBuilder.AddColumn<Guid>(
                name: "StationId",
                schema: "hcs_bus_management",
                table: "VehicleLegalDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PremisesUnitId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceReference",
                schema: "hcs_bus_management",
                table: "RevenueReceipts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehiclePlateNumber",
                schema: "hcs_bus_management",
                table: "RevenueReceipts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAtUtc",
                schema: "hcs_bus_management",
                table: "AdjustmentEntries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                schema: "hcs_bus_management",
                table: "AdjustmentEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleLegalDocuments_StationId_ExpiresOn_IsActive",
                schema: "hcs_bus_management",
                table: "VehicleLegalDocuments",
                columns: new[] { "StationId", "ExpiresOn", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AdjustmentEntries_StationId_Status",
                schema: "hcs_bus_management",
                table: "AdjustmentEntries",
                columns: new[] { "StationId", "Status" });

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "hcs_bus_management"."AdjustmentEntries"
                        WHERE (("ReceiptId" IS NOT NULL) = ("ExpenseId" IS NOT NULL)) OR "Amount" = 0
                    ) THEN
                        RAISE EXCEPTION 'Cannot add adjustment integrity constraint: existing invalid AdjustmentEntries rows were found.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AdjustmentEntry_ExactlyOneTarget",
                schema: "hcs_bus_management",
                table: "AdjustmentEntries",
                sql: "(\"ReceiptId\" IS NOT NULL) <> (\"ExpenseId\" IS NOT NULL) AND \"Amount\" <> 0");

            migrationBuilder.AddForeignKey(
                name: "FK_VehicleLegalDocuments_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "VehicleLegalDocuments",
                column: "StationId",
                principalSchema: "hcs_bus_management",
                principalTable: "BusStations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VehicleLegalDocuments_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "VehicleLegalDocuments");

            migrationBuilder.DropIndex(
                name: "IX_VehicleLegalDocuments_StationId_ExpiresOn_IsActive",
                schema: "hcs_bus_management",
                table: "VehicleLegalDocuments");

            migrationBuilder.DropIndex(
                name: "IX_AdjustmentEntries_StationId_Status",
                schema: "hcs_bus_management",
                table: "AdjustmentEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AdjustmentEntry_ExactlyOneTarget",
                schema: "hcs_bus_management",
                table: "AdjustmentEntries");

            migrationBuilder.DropColumn(
                name: "StationId",
                schema: "hcs_bus_management",
                table: "VehicleLegalDocuments");

            migrationBuilder.DropColumn(
                name: "PremisesUnitId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts");

            migrationBuilder.DropColumn(
                name: "SourceReference",
                schema: "hcs_bus_management",
                table: "RevenueReceipts");

            migrationBuilder.DropColumn(
                name: "VehiclePlateNumber",
                schema: "hcs_bus_management",
                table: "RevenueReceipts");

            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                schema: "hcs_bus_management",
                table: "AdjustmentEntries");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                schema: "hcs_bus_management",
                table: "AdjustmentEntries");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleLegalDocuments_ExpiresOn_IsActive",
                schema: "hcs_bus_management",
                table: "VehicleLegalDocuments",
                columns: new[] { "ExpiresOn", "IsActive" });
        }
    }
}
