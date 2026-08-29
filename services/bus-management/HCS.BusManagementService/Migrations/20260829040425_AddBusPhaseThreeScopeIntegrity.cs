using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.BusManagementService.Migrations
{
    /// <inheritdoc />
    public partial class AddBusPhaseThreeScopeIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StationId",
                schema: "hcs_bus_management",
                table: "Vehicles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StationId",
                schema: "hcs_bus_management",
                table: "TransportOperators",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StationId",
                schema: "hcs_bus_management",
                table: "FixedRoutes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StationId",
                schema: "hcs_bus_management",
                table: "Drivers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PremisesUnits_Id_StationId",
                schema: "hcs_bus_management",
                table: "PremisesUnits",
                columns: new[] { "Id", "StationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_StationId",
                schema: "hcs_bus_management",
                table: "Vehicles",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_TransportOperators_StationId",
                schema: "hcs_bus_management",
                table: "TransportOperators",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueReceipts_PremisesUnitId_StationId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts",
                columns: new[] { "PremisesUnitId", "StationId" });

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "hcs_bus_management"."RevenueReceipts"
                        WHERE "SourceType" NOT IN ('FixedRoute', 'VisitingVehicle', 'PublicBus', 'Parking', 'Premises', 'Other')
                           OR ("SourceType" = 'Premises' AND "PremisesUnitId" IS NULL)
                    ) THEN
                        RAISE EXCEPTION 'Cannot add revenue source integrity constraint: existing invalid RevenueReceipts rows were found.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_RevenueReceipt_SourceType",
                schema: "hcs_bus_management",
                table: "RevenueReceipts",
                sql: "\"SourceType\" IN ('FixedRoute', 'VisitingVehicle', 'PublicBus', 'Parking', 'Premises', 'Other') AND (\"SourceType\" <> 'Premises' OR \"PremisesUnitId\" IS NOT NULL)");

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "hcs_bus_management"."RevenueReceipts" receipt
                        LEFT JOIN "hcs_bus_management"."PremisesUnits" premises
                          ON premises."Id" = receipt."PremisesUnitId"
                         AND premises."StationId" = receipt."StationId"
                        WHERE receipt."PremisesUnitId" IS NOT NULL AND premises."Id" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot add premises ownership FK: existing RevenueReceipts reference a missing or cross-station PremisesUnit.';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_FixedRoutes_StationId",
                schema: "hcs_bus_management",
                table: "FixedRoutes",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_StationId",
                schema: "hcs_bus_management",
                table: "Drivers",
                column: "StationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Drivers_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "Drivers",
                column: "StationId",
                principalSchema: "hcs_bus_management",
                principalTable: "BusStations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FixedRoutes_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "FixedRoutes",
                column: "StationId",
                principalSchema: "hcs_bus_management",
                principalTable: "BusStations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RevenueReceipts_PremisesUnits_PremisesUnitId_StationId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts",
                columns: new[] { "PremisesUnitId", "StationId" },
                principalSchema: "hcs_bus_management",
                principalTable: "PremisesUnits",
                principalColumns: new[] { "Id", "StationId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TransportOperators_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "TransportOperators",
                column: "StationId",
                principalSchema: "hcs_bus_management",
                principalTable: "BusStations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "Vehicles",
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
                name: "FK_Drivers_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "Drivers");

            migrationBuilder.DropForeignKey(
                name: "FK_FixedRoutes_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "FixedRoutes");

            migrationBuilder.DropForeignKey(
                name: "FK_RevenueReceipts_PremisesUnits_PremisesUnitId_StationId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_TransportOperators_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "TransportOperators");

            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_StationId",
                schema: "hcs_bus_management",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_TransportOperators_StationId",
                schema: "hcs_bus_management",
                table: "TransportOperators");

            migrationBuilder.DropIndex(
                name: "IX_RevenueReceipts_PremisesUnitId_StationId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RevenueReceipt_SourceType",
                schema: "hcs_bus_management",
                table: "RevenueReceipts");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PremisesUnits_Id_StationId",
                schema: "hcs_bus_management",
                table: "PremisesUnits");

            migrationBuilder.DropIndex(
                name: "IX_FixedRoutes_StationId",
                schema: "hcs_bus_management",
                table: "FixedRoutes");

            migrationBuilder.DropIndex(
                name: "IX_Drivers_StationId",
                schema: "hcs_bus_management",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "StationId",
                schema: "hcs_bus_management",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "StationId",
                schema: "hcs_bus_management",
                table: "TransportOperators");

            migrationBuilder.DropColumn(
                name: "StationId",
                schema: "hcs_bus_management",
                table: "FixedRoutes");

            migrationBuilder.DropColumn(
                name: "StationId",
                schema: "hcs_bus_management",
                table: "Drivers");
        }
    }
}
