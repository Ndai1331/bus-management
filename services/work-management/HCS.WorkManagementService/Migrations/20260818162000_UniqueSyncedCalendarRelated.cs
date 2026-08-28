using HCS.WorkManagementService.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.WorkManagementService.Migrations
{
    [DbContext(typeof(WorkManagementDbContext))]
    [Migration("20260818162000_UniqueSyncedCalendarRelated")]
    public partial class UniqueSyncedCalendarRelated : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_RelatedType_RelatedId",
                schema: "hcs_work",
                table: "CalendarEvents");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_SyncedRelated",
                schema: "hcs_work",
                table: "CalendarEvents",
                columns: new[] { "RelatedType", "RelatedId", "EventType" },
                unique: true,
                filter: "\"RelatedId\" IS NOT NULL AND \"EventType\" IN ('PROJECT', 'TASK')");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_SyncedRelated",
                schema: "hcs_work",
                table: "CalendarEvents");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_RelatedType_RelatedId",
                schema: "hcs_work",
                table: "CalendarEvents",
                columns: new[] { "RelatedType", "RelatedId" });
        }
    }
}
