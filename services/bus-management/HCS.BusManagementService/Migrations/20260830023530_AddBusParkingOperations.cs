using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.BusManagementService.Migrations
{
    /// <inheritdoc />
    public partial class AddBusParkingOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParkingReservationId",
                schema: "hcs_bus_management",
                table: "ParkingSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParkingSpotId",
                schema: "hcs_bus_management",
                table: "ParkingSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ParkingSpots",
                schema: "hcs_bus_management",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    VehicleType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_ParkingSpots", x => x.Id);
                    table.UniqueConstraint("AK_ParkingSpots_Id_StationId", x => new { x.Id, x.StationId });
                    table.ForeignKey(
                        name: "FK_ParkingSpots_BusStations_StationId",
                        column: x => x.StationId,
                        principalSchema: "hcs_bus_management",
                        principalTable: "BusStations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParkingReservations",
                schema: "hcs_bus_management",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParkingSpotId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehiclePlateNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VehicleType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("PK_ParkingReservations", x => x.Id);
                    table.UniqueConstraint("AK_ParkingReservations_Id_StationId", x => new { x.Id, x.StationId });
                    table.ForeignKey(
                        name: "FK_ParkingReservations_BusStations_StationId",
                        column: x => x.StationId,
                        principalSchema: "hcs_bus_management",
                        principalTable: "BusStations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParkingReservations_ParkingSpots_ParkingSpotId_StationId",
                        columns: x => new { x.ParkingSpotId, x.StationId },
                        principalSchema: "hcs_bus_management",
                        principalTable: "ParkingSpots",
                        principalColumns: new[] { "Id", "StationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSessions_ParkingReservationId",
                schema: "hcs_bus_management",
                table: "ParkingSessions",
                column: "ParkingReservationId",
                unique: true,
                filter: "\"ParkingReservationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSessions_ParkingReservationId_StationId",
                schema: "hcs_bus_management",
                table: "ParkingSessions",
                columns: new[] { "ParkingReservationId", "StationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSessions_ParkingSpotId_StationId",
                schema: "hcs_bus_management",
                table: "ParkingSessions",
                columns: new[] { "ParkingSpotId", "StationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingReservations_ParkingSpotId_StartUtc_Status",
                schema: "hcs_bus_management",
                table: "ParkingReservations",
                columns: new[] { "ParkingSpotId", "StartUtc", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingReservations_ParkingSpotId_StationId",
                schema: "hcs_bus_management",
                table: "ParkingReservations",
                columns: new[] { "ParkingSpotId", "StationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingReservations_StationId_StartUtc_EndUtc",
                schema: "hcs_bus_management",
                table: "ParkingReservations",
                columns: new[] { "StationId", "StartUtc", "EndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingReservations_StationId_VehiclePlateNumber_StartUtc_S~",
                schema: "hcs_bus_management",
                table: "ParkingReservations",
                columns: new[] { "StationId", "VehiclePlateNumber", "StartUtc", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSpots_StationId_Code",
                schema: "hcs_bus_management",
                table: "ParkingSpots",
                columns: new[] { "StationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSpots_StationId_IsActive",
                schema: "hcs_bus_management",
                table: "ParkingSpots",
                columns: new[] { "StationId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_ParkingSessions_ParkingReservations_ParkingReservationId_St~",
                schema: "hcs_bus_management",
                table: "ParkingSessions",
                columns: new[] { "ParkingReservationId", "StationId" },
                principalSchema: "hcs_bus_management",
                principalTable: "ParkingReservations",
                principalColumns: new[] { "Id", "StationId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ParkingSessions_ParkingSpots_ParkingSpotId_StationId",
                schema: "hcs_bus_management",
                table: "ParkingSessions",
                columns: new[] { "ParkingSpotId", "StationId" },
                principalSchema: "hcs_bus_management",
                principalTable: "ParkingSpots",
                principalColumns: new[] { "Id", "StationId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ParkingSessions_ParkingReservations_ParkingReservationId_St~",
                schema: "hcs_bus_management",
                table: "ParkingSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_ParkingSessions_ParkingSpots_ParkingSpotId_StationId",
                schema: "hcs_bus_management",
                table: "ParkingSessions");

            migrationBuilder.DropTable(
                name: "ParkingReservations",
                schema: "hcs_bus_management");

            migrationBuilder.DropTable(
                name: "ParkingSpots",
                schema: "hcs_bus_management");

            migrationBuilder.DropIndex(
                name: "IX_ParkingSessions_ParkingReservationId",
                schema: "hcs_bus_management",
                table: "ParkingSessions");

            migrationBuilder.DropIndex(
                name: "IX_ParkingSessions_ParkingReservationId_StationId",
                schema: "hcs_bus_management",
                table: "ParkingSessions");

            migrationBuilder.DropIndex(
                name: "IX_ParkingSessions_ParkingSpotId_StationId",
                schema: "hcs_bus_management",
                table: "ParkingSessions");

            migrationBuilder.DropColumn(
                name: "ParkingReservationId",
                schema: "hcs_bus_management",
                table: "ParkingSessions");

            migrationBuilder.DropColumn(
                name: "ParkingSpotId",
                schema: "hcs_bus_management",
                table: "ParkingSessions");
        }
    }
}
