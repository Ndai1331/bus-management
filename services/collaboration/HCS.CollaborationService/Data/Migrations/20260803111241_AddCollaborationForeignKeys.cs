using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.CollaborationService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollaborationForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_CollaborationAttachments_CollaborationConversations_Convers~",
                table: "CollaborationAttachments",
                column: "ConversationId",
                principalTable: "CollaborationConversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CollaborationMessages_CollaborationConversations_Conversati~",
                table: "CollaborationMessages",
                column: "ConversationId",
                principalTable: "CollaborationConversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CollaborationNotificationReceivers_CollaborationNotificatio~",
                table: "CollaborationNotificationReceivers",
                column: "NotificationId",
                principalTable: "CollaborationNotifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CollaborationAttachments_CollaborationConversations_Convers~",
                table: "CollaborationAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_CollaborationMessages_CollaborationConversations_Conversati~",
                table: "CollaborationMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_CollaborationNotificationReceivers_CollaborationNotificatio~",
                table: "CollaborationNotificationReceivers");
        }
    }
}
