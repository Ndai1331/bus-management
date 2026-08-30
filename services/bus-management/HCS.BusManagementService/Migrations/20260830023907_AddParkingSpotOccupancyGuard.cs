using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.BusManagementService.Migrations
{
    /// <inheritdoc />
    public partial class AddParkingSpotOccupancyGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ParkingSessions_StationId_ParkingSpotId",
                schema: "hcs_bus_management",
                table: "ParkingSessions",
                columns: new[] { "StationId", "ParkingSpotId" },
                unique: true,
                filter: "\"Status\" = 'Open' AND \"ParkingSpotId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ParkingSessions_StationId_ParkingSpotId",
                schema: "hcs_bus_management",
                table: "ParkingSessions");
        }
    }
}
