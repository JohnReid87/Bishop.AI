using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bishop.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceIdToBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Batches_BranchName",
                table: "Batches");

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "Batches",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "NOCASE");

            // Assign existing batches to the first available workspace.
            // If no workspace exists, delete the orphaned batches so the FK
            // constraint is satisfied during the SQLite table rebuild below.
            migrationBuilder.Sql(@"
                UPDATE Batches
                SET WorkspaceId = (
                    SELECT Id FROM Workspaces
                    WHERE IsRemoved = 0
                    ORDER BY Position ASC
                    LIMIT 1
                )
                WHERE WorkspaceId = '00000000-0000-0000-0000-000000000000'
                  AND EXISTS (SELECT 1 FROM Workspaces WHERE IsRemoved = 0);

                DELETE FROM Batches
                WHERE WorkspaceId = '00000000-0000-0000-0000-000000000000';
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Batches_WorkspaceId_BranchName",
                table: "Batches",
                columns: new[] { "WorkspaceId", "BranchName" },
                unique: true,
                filter: "Status != 2");

            migrationBuilder.AddForeignKey(
                name: "FK_Batches_Workspaces_WorkspaceId",
                table: "Batches",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Batches_Workspaces_WorkspaceId",
                table: "Batches");

            migrationBuilder.DropIndex(
                name: "IX_Batches_WorkspaceId_BranchName",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "Batches");

            migrationBuilder.CreateIndex(
                name: "IX_Batches_BranchName",
                table: "Batches",
                column: "BranchName",
                unique: true,
                filter: "Status != 2");
        }
    }
}
