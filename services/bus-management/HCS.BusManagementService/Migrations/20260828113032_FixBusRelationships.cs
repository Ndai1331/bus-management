using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.BusManagementService.Migrations
{
    /// <inheritdoc />
    public partial class FixBusRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RevenueLines_RevenueReceipts_RevenueReceiptId",
                schema: "hcs_bus_management",
                table: "RevenueLines");

            migrationBuilder.DropIndex(
                name: "IX_RevenueLines_RevenueReceiptId",
                schema: "hcs_bus_management",
                table: "RevenueLines");

            migrationBuilder.DropColumn(
                name: "RevenueReceiptId",
                schema: "hcs_bus_management",
                table: "RevenueLines");

            migrationBuilder.CreateIndex(
                name: "IX_Tariffs_RouteId",
                schema: "hcs_bus_management",
                table: "Tariffs",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueReceipts_DepartureId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts",
                column: "DepartureId");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueReceipts_OperatorId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueLines_TariffId",
                schema: "hcs_bus_management",
                table: "RevenueLines",
                column: "TariffId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartureTrips_DriverId",
                schema: "hcs_bus_management",
                table: "DepartureTrips",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartureTrips_OperatorId",
                schema: "hcs_bus_management",
                table: "DepartureTrips",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartureTrips_RouteId",
                schema: "hcs_bus_management",
                table: "DepartureTrips",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartureTrips_VehicleId",
                schema: "hcs_bus_management",
                table: "DepartureTrips",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_CarrierContracts_OperatorId",
                schema: "hcs_bus_management",
                table: "CarrierContracts",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_AdjustmentEntries_ExpenseId",
                schema: "hcs_bus_management",
                table: "AdjustmentEntries",
                column: "ExpenseId");

            migrationBuilder.CreateIndex(
                name: "IX_AdjustmentEntries_ReceiptId",
                schema: "hcs_bus_management",
                table: "AdjustmentEntries",
                column: "ReceiptId");

            migrationBuilder.AddForeignKey(
                name: "FK_AdjustmentEntries_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "AdjustmentEntries",
                column: "StationId",
                principalSchema: "hcs_bus_management",
                principalTable: "BusStations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AdjustmentEntries_ExpenseEntries_ExpenseId",
                schema: "hcs_bus_management",
                table: "AdjustmentEntries",
                column: "ExpenseId",
                principalSchema: "hcs_bus_management",
                principalTable: "ExpenseEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AdjustmentEntries_RevenueReceipts_ReceiptId",
                schema: "hcs_bus_management",
                table: "AdjustmentEntries",
                column: "ReceiptId",
                principalSchema: "hcs_bus_management",
                principalTable: "RevenueReceipts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CarrierContracts_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "CarrierContracts",
                column: "StationId",
                principalSchema: "hcs_bus_management",
                principalTable: "BusStations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CarrierContracts_TransportOperators_OperatorId",
                schema: "hcs_bus_management",
                table: "CarrierContracts",
                column: "OperatorId",
                principalSchema: "hcs_bus_management",
                principalTable: "TransportOperators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyCloses_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "DailyCloses",
                column: "StationId",
                principalSchema: "hcs_bus_management",
                principalTable: "BusStations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DepartureTrips_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "DepartureTrips",
                column: "StationId",
                principalSchema: "hcs_bus_management",
                principalTable: "BusStations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DepartureTrips_Drivers_DriverId",
                schema: "hcs_bus_management",
                table: "DepartureTrips",
                column: "DriverId",
                principalSchema: "hcs_bus_management",
                principalTable: "Drivers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DepartureTrips_FixedRoutes_RouteId",
                schema: "hcs_bus_management",
                table: "DepartureTrips",
                column: "RouteId",
                principalSchema: "hcs_bus_management",
                principalTable: "FixedRoutes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DepartureTrips_TransportOperators_OperatorId",
                schema: "hcs_bus_management",
                table: "DepartureTrips",
                column: "OperatorId",
                principalSchema: "hcs_bus_management",
                principalTable: "TransportOperators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DepartureTrips_Vehicles_VehicleId",
                schema: "hcs_bus_management",
                table: "DepartureTrips",
                column: "VehicleId",
                principalSchema: "hcs_bus_management",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseEntries_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "ExpenseEntries",
                column: "StationId",
                principalSchema: "hcs_bus_management",
                principalTable: "BusStations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FixedRoutes_TransportOperators_OperatorId",
                schema: "hcs_bus_management",
                table: "FixedRoutes",
                column: "OperatorId",
                principalSchema: "hcs_bus_management",
                principalTable: "TransportOperators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaseContracts_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "LeaseContracts",
                column: "StationId",
                principalSchema: "hcs_bus_management",
                principalTable: "BusStations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PremisesUnits_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "PremisesUnits",
                column: "StationId",
                principalSchema: "hcs_bus_management",
                principalTable: "BusStations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RevenueLines_Tariffs_TariffId",
                schema: "hcs_bus_management",
                table: "RevenueLines",
                column: "TariffId",
                principalSchema: "hcs_bus_management",
                principalTable: "Tariffs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RevenueReceipts_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts",
                column: "StationId",
                principalSchema: "hcs_bus_management",
                principalTable: "BusStations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RevenueReceipts_DepartureTrips_DepartureId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts",
                column: "DepartureId",
                principalSchema: "hcs_bus_management",
                principalTable: "DepartureTrips",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RevenueReceipts_TransportOperators_OperatorId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts",
                column: "OperatorId",
                principalSchema: "hcs_bus_management",
                principalTable: "TransportOperators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftSettlements_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "ShiftSettlements",
                column: "StationId",
                principalSchema: "hcs_bus_management",
                principalTable: "BusStations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tariffs_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "Tariffs",
                column: "StationId",
                principalSchema: "hcs_bus_management",
                principalTable: "BusStations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tariffs_FixedRoutes_RouteId",
                schema: "hcs_bus_management",
                table: "Tariffs",
                column: "RouteId",
                principalSchema: "hcs_bus_management",
                principalTable: "FixedRoutes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_TransportOperators_OperatorId",
                schema: "hcs_bus_management",
                table: "Vehicles",
                column: "OperatorId",
                principalSchema: "hcs_bus_management",
                principalTable: "TransportOperators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdjustmentEntries_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "AdjustmentEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_AdjustmentEntries_ExpenseEntries_ExpenseId",
                schema: "hcs_bus_management",
                table: "AdjustmentEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_AdjustmentEntries_RevenueReceipts_ReceiptId",
                schema: "hcs_bus_management",
                table: "AdjustmentEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_CarrierContracts_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "CarrierContracts");

            migrationBuilder.DropForeignKey(
                name: "FK_CarrierContracts_TransportOperators_OperatorId",
                schema: "hcs_bus_management",
                table: "CarrierContracts");

            migrationBuilder.DropForeignKey(
                name: "FK_DailyCloses_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "DailyCloses");

            migrationBuilder.DropForeignKey(
                name: "FK_DepartureTrips_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "DepartureTrips");

            migrationBuilder.DropForeignKey(
                name: "FK_DepartureTrips_Drivers_DriverId",
                schema: "hcs_bus_management",
                table: "DepartureTrips");

            migrationBuilder.DropForeignKey(
                name: "FK_DepartureTrips_FixedRoutes_RouteId",
                schema: "hcs_bus_management",
                table: "DepartureTrips");

            migrationBuilder.DropForeignKey(
                name: "FK_DepartureTrips_TransportOperators_OperatorId",
                schema: "hcs_bus_management",
                table: "DepartureTrips");

            migrationBuilder.DropForeignKey(
                name: "FK_DepartureTrips_Vehicles_VehicleId",
                schema: "hcs_bus_management",
                table: "DepartureTrips");

            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseEntries_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "ExpenseEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_FixedRoutes_TransportOperators_OperatorId",
                schema: "hcs_bus_management",
                table: "FixedRoutes");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaseContracts_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "LeaseContracts");

            migrationBuilder.DropForeignKey(
                name: "FK_PremisesUnits_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "PremisesUnits");

            migrationBuilder.DropForeignKey(
                name: "FK_RevenueLines_Tariffs_TariffId",
                schema: "hcs_bus_management",
                table: "RevenueLines");

            migrationBuilder.DropForeignKey(
                name: "FK_RevenueReceipts_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_RevenueReceipts_DepartureTrips_DepartureId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_RevenueReceipts_TransportOperators_OperatorId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftSettlements_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "ShiftSettlements");

            migrationBuilder.DropForeignKey(
                name: "FK_Tariffs_BusStations_StationId",
                schema: "hcs_bus_management",
                table: "Tariffs");

            migrationBuilder.DropForeignKey(
                name: "FK_Tariffs_FixedRoutes_RouteId",
                schema: "hcs_bus_management",
                table: "Tariffs");

            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_TransportOperators_OperatorId",
                schema: "hcs_bus_management",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Tariffs_RouteId",
                schema: "hcs_bus_management",
                table: "Tariffs");

            migrationBuilder.DropIndex(
                name: "IX_RevenueReceipts_DepartureId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts");

            migrationBuilder.DropIndex(
                name: "IX_RevenueReceipts_OperatorId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts");

            migrationBuilder.DropIndex(
                name: "IX_RevenueLines_TariffId",
                schema: "hcs_bus_management",
                table: "RevenueLines");

            migrationBuilder.DropIndex(
                name: "IX_DepartureTrips_DriverId",
                schema: "hcs_bus_management",
                table: "DepartureTrips");

            migrationBuilder.DropIndex(
                name: "IX_DepartureTrips_OperatorId",
                schema: "hcs_bus_management",
                table: "DepartureTrips");

            migrationBuilder.DropIndex(
                name: "IX_DepartureTrips_RouteId",
                schema: "hcs_bus_management",
                table: "DepartureTrips");

            migrationBuilder.DropIndex(
                name: "IX_DepartureTrips_VehicleId",
                schema: "hcs_bus_management",
                table: "DepartureTrips");

            migrationBuilder.DropIndex(
                name: "IX_CarrierContracts_OperatorId",
                schema: "hcs_bus_management",
                table: "CarrierContracts");

            migrationBuilder.DropIndex(
                name: "IX_AdjustmentEntries_ExpenseId",
                schema: "hcs_bus_management",
                table: "AdjustmentEntries");

            migrationBuilder.DropIndex(
                name: "IX_AdjustmentEntries_ReceiptId",
                schema: "hcs_bus_management",
                table: "AdjustmentEntries");

            migrationBuilder.AddColumn<Guid>(
                name: "RevenueReceiptId",
                schema: "hcs_bus_management",
                table: "RevenueLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RevenueLines_RevenueReceiptId",
                schema: "hcs_bus_management",
                table: "RevenueLines",
                column: "RevenueReceiptId");

            migrationBuilder.AddForeignKey(
                name: "FK_RevenueLines_RevenueReceipts_RevenueReceiptId",
                schema: "hcs_bus_management",
                table: "RevenueLines",
                column: "RevenueReceiptId",
                principalSchema: "hcs_bus_management",
                principalTable: "RevenueReceipts",
                principalColumn: "Id");
        }
    }
}
