using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.CollaborationService.Data.Migrations
{
    /// <inheritdoc />
    public partial class ClearDeletedWorkSubjectMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "CollaborationWorkSubjectMembers" AS member
                USING "CollaborationWorkSubjects" AS subject
                WHERE member."SubjectId" = subject."Id"
                  AND subject."IsDeleted" = TRUE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deleted projection memberships are intentionally irreversible.
        }
    }
}
