using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.CollaborationService.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApplyStrictTaskConversationShape : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversation_SubjectShape",
                table: "CollaborationConversations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversation_SubjectShape",
                table: "CollaborationConversations",
                sql: "(\"Type\" IN (0,1) AND \"ProjectId\" IS NULL AND \"TaskId\" IS NULL) OR (\"Type\" = 2 AND \"ProjectId\" IS NOT NULL AND \"TaskId\" IS NULL) OR (\"Type\" = 3 AND \"ProjectId\" IS NULL AND \"TaskId\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversation_SubjectShape",
                table: "CollaborationConversations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversation_SubjectShape",
                table: "CollaborationConversations",
                sql: "(\"Type\" IN (0,1) AND \"ProjectId\" IS NULL AND \"TaskId\" IS NULL) OR (\"Type\" = 2 AND \"ProjectId\" IS NOT NULL AND \"TaskId\" IS NULL) OR (\"Type\" = 3 AND \"ProjectId\" IS NOT NULL AND \"TaskId\" IS NOT NULL)");
        }
    }
}
