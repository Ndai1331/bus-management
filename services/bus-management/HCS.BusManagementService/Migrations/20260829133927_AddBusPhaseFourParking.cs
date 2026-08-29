using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.BusManagementService.Migrations
{
    /// <inheritdoc />
    public partial class AddBusPhaseFourParking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_RevenueReceipt_SourceType",
                schema: "hcs_bus_management",
                table: "RevenueReceipts");

            migrationBuilder.AddColumn<bool>(
                name: "IsLegacyParking",
                schema: "hcs_bus_management",
                table: "RevenueReceipts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ParkingSessionId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ParkingTariffs",
                schema: "hcs_bus_management",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StationId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BillingUnitMinutes = table.Column<int>(type: "integer", nullable: false),
                    RatePerUnit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimumCharge = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParkingTariffs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParkingTariffs_BusStations_StationId",
                        column: x => x.StationId,
                        principalSchema: "hcs_bus_management",
                        principalTable: "BusStations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParkingSessions",
                schema: "hcs_bus_management",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessDate = table.Column<DateTime>(type: "date", nullable: false),
                    ShiftCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VehiclePlateNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VehicleType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ArrivalUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExitUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    BilledUnits = table.Column<int>(type: "integer", nullable: true),
                    ParkingTariffId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillingUnitMinutes = table.Column<int>(type: "integer", nullable: false),
                    RatePerUnit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimumCharge = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TariffDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ChargedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CancellationReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParkingSessions", x => x.Id);
                    table.UniqueConstraint("AK_ParkingSessions_Id_StationId", x => new { x.Id, x.StationId });
                    table.ForeignKey(
                        name: "FK_ParkingSessions_BusStations_StationId",
                        column: x => x.StationId,
                        principalSchema: "hcs_bus_management",
                        principalTable: "BusStations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParkingSessions_ParkingTariffs_ParkingTariffId",
                        column: x => x.ParkingTariffId,
                        principalSchema: "hcs_bus_management",
                        principalTable: "ParkingTariffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RevenueReceipts_ParkingSessionId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts",
                column: "ParkingSessionId",
                unique: true,
                filter: "\"ParkingSessionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueReceipts_ParkingSessionId_StationId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts",
                columns: new[] { "ParkingSessionId", "StationId" });

            // Preserve historical Parking receipts that predate ParkingSession.
            // New receipts are still required to reference a ParkingSession by the domain and API.
            migrationBuilder.Sql("""
                UPDATE "hcs_bus_management"."RevenueReceipts"
                SET "IsLegacyParking" = TRUE
                WHERE "SourceType" = 'Parking' AND "ParkingSessionId" IS NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_RevenueReceipt_SourceType",
                schema: "hcs_bus_management",
                table: "RevenueReceipts",
                sql: "\"SourceType\" IN ('FixedRoute', 'VisitingVehicle', 'PublicBus', 'Parking', 'Premises', 'Other') AND (\"SourceType\" <> 'Premises' OR \"PremisesUnitId\" IS NOT NULL) AND ((\"SourceType\" = 'Parking' AND ((\"ParkingSessionId\" IS NOT NULL AND \"IsLegacyParking\" = FALSE) OR (\"ParkingSessionId\" IS NULL AND \"IsLegacyParking\" = TRUE))) OR (\"SourceType\" <> 'Parking' AND \"ParkingSessionId\" IS NULL AND \"IsLegacyParking\" = FALSE))");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSessions_ParkingTariffId",
                schema: "hcs_bus_management",
                table: "ParkingSessions",
                column: "ParkingTariffId");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSessions_StationId_BusinessDate_Status",
                schema: "hcs_bus_management",
                table: "ParkingSessions",
                columns: new[] { "StationId", "BusinessDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSessions_StationId_BusinessDate_VehiclePlateNumber",
                schema: "hcs_bus_management",
                table: "ParkingSessions",
                columns: new[] { "StationId", "BusinessDate", "VehiclePlateNumber" },
                unique: true,
                filter: "\"Status\" = 'Open'");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingTariffs_StationId_VehicleType_EffectiveFrom",
                schema: "hcs_bus_management",
                table: "ParkingTariffs",
                columns: new[] { "StationId", "VehicleType", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParkingTariffs_StationId_VehicleType_EffectiveFrom_Effectiv~",
                schema: "hcs_bus_management",
                table: "ParkingTariffs",
                columns: new[] { "StationId", "VehicleType", "EffectiveFrom", "EffectiveTo", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_RevenueReceipts_ParkingSessions_ParkingSessionId_StationId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts",
                columns: new[] { "ParkingSessionId", "StationId" },
                principalSchema: "hcs_bus_management",
                principalTable: "ParkingSessions",
                principalColumns: new[] { "Id", "StationId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RevenueReceipts_ParkingSessions_ParkingSessionId_StationId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts");

            migrationBuilder.DropTable(
                name: "ParkingSessions",
                schema: "hcs_bus_management");

            migrationBuilder.DropTable(
                name: "ParkingTariffs",
                schema: "hcs_bus_management");

            migrationBuilder.DropIndex(
                name: "IX_RevenueReceipts_ParkingSessionId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts");

            migrationBuilder.DropIndex(
                name: "IX_RevenueReceipts_ParkingSessionId_StationId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RevenueReceipt_SourceType",
                schema: "hcs_bus_management",
                table: "RevenueReceipts");

            migrationBuilder.DropColumn(
                name: "IsLegacyParking",
                schema: "hcs_bus_management",
                table: "RevenueReceipts");

            migrationBuilder.DropColumn(
                name: "ParkingSessionId",
                schema: "hcs_bus_management",
                table: "RevenueReceipts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RevenueReceipt_SourceType",
                schema: "hcs_bus_management",
                table: "RevenueReceipts",
                sql: "\"SourceType\" IN ('FixedRoute', 'VisitingVehicle', 'PublicBus', 'Parking', 'Premises', 'Other') AND (\"SourceType\" <> 'Premises' OR \"PremisesUnitId\" IS NOT NULL)");
        }
    }
}
