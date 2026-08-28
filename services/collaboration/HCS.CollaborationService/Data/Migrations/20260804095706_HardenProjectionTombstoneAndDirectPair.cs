using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.CollaborationService.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardenProjectionTombstoneAndDirectPair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CollaborationWorkSubjects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "DirectUserHighId",
                table: "CollaborationConversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DirectUserLowId",
                table: "CollaborationConversations",
                type: "uuid",
                nullable: true);

            // Preserve existing direct chats before the unique pair constraint is
            // added. Malformed historic rows remain null and are not treated as
            // canonical conversations by the new API.
            migrationBuilder.Sql("""
                WITH pairs AS (
                    SELECT "ConversationId", array_agg("UserId" ORDER BY "UserId") AS users
                    FROM "CollaborationConversationMembers"
                    GROUP BY "ConversationId"
                    HAVING count(*) = 2
                ), canonical AS (
                    SELECT c."Id", pairs.users,
                        row_number() OVER (PARTITION BY pairs.users ORDER BY c."Id") AS rank
                    FROM "CollaborationConversations" AS c
                    JOIN pairs ON pairs."ConversationId" = c."Id"
                    WHERE c."Type" = 0
                )
                UPDATE "CollaborationConversations" AS c
                SET "DirectUserLowId" = canonical.users[1], "DirectUserHighId" = canonical.users[2]
                FROM canonical
                WHERE c."Id" = canonical."Id" AND canonical.rank = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationWorkSubjects_SubjectType_IsDeleted_LastOccurre~",
                table: "CollaborationWorkSubjects",
                columns: new[] { "SubjectType", "IsDeleted", "LastOccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationConversations_DirectUserLowId_DirectUserHighId",
                table: "CollaborationConversations",
                columns: new[] { "DirectUserLowId", "DirectUserHighId" },
                unique: true,
                filter: "\"Type\" = 0 AND \"DirectUserLowId\" IS NOT NULL AND \"DirectUserHighId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CollaborationWorkSubjects_SubjectType_IsDeleted_LastOccurre~",
                table: "CollaborationWorkSubjects");

            migrationBuilder.DropIndex(
                name: "IX_CollaborationConversations_DirectUserLowId_DirectUserHighId",
                table: "CollaborationConversations");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CollaborationWorkSubjects");

            migrationBuilder.DropColumn(
                name: "DirectUserHighId",
                table: "CollaborationConversations");

            migrationBuilder.DropColumn(
                name: "DirectUserLowId",
                table: "CollaborationConversations");
        }
    }
}
