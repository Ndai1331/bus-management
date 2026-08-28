using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCS.DocumentService.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeDocumentSigningQueries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_Status_CreationTime",
                schema: "document",
                table: "WorkflowInstances",
                columns: new[] { "Status", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentHistories_ActorUserId_Action_DocumentId",
                schema: "document",
                table: "DocumentHistories",
                columns: new[] { "ActorUserId", "Action", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAssignments_AssigneeUserId_IsCurrent_Responsibility~",
                schema: "document",
                table: "DocumentAssignments",
                columns: new[] { "AssigneeUserId", "IsCurrent", "Responsibility", "StepCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalTasks_AssigneeUserId_Status_InstanceId",
                schema: "document",
                table: "ApprovalTasks",
                columns: new[] { "AssigneeUserId", "Status", "InstanceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_Status_CreationTime",
                schema: "document",
                table: "WorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_DocumentHistories_ActorUserId_Action_DocumentId",
                schema: "document",
                table: "DocumentHistories");

            migrationBuilder.DropIndex(
                name: "IX_DocumentAssignments_AssigneeUserId_IsCurrent_Responsibility~",
                schema: "document",
                table: "DocumentAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalTasks_AssigneeUserId_Status_InstanceId",
                schema: "document",
                table: "ApprovalTasks");
        }
    }
}
