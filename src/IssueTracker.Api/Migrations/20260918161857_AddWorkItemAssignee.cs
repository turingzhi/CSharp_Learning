using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssueTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemAssignee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssigneeId",
                table: "WorkItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_AssigneeId",
                table: "WorkItems",
                column: "AssigneeId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_AspNetUsers_AssigneeId",
                table: "WorkItems",
                column: "AssigneeId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_AspNetUsers_AssigneeId",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_AssigneeId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "AssigneeId",
                table: "WorkItems");
        }
    }
}
