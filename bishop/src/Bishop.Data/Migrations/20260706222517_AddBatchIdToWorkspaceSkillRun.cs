using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bishop.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchIdToWorkspaceSkillRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkspaceSkillRuns_WorkspaceId_SkillName_ProjectName",
                table: "WorkspaceSkillRuns");

            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "WorkspaceSkillRuns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceSkillRuns_BatchId",
                table: "WorkspaceSkillRuns",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceSkillRuns_WorkspaceId_SkillName_ProjectName_BatchId",
                table: "WorkspaceSkillRuns",
                columns: new[] { "WorkspaceId", "SkillName", "ProjectName", "BatchId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkspaceSkillRuns_Batches_BatchId",
                table: "WorkspaceSkillRuns",
                column: "BatchId",
                principalTable: "Batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkspaceSkillRuns_Batches_BatchId",
                table: "WorkspaceSkillRuns");

            migrationBuilder.DropIndex(
                name: "IX_WorkspaceSkillRuns_BatchId",
                table: "WorkspaceSkillRuns");

            migrationBuilder.DropIndex(
                name: "IX_WorkspaceSkillRuns_WorkspaceId_SkillName_ProjectName_BatchId",
                table: "WorkspaceSkillRuns");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "WorkspaceSkillRuns");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceSkillRuns_WorkspaceId_SkillName_ProjectName",
                table: "WorkspaceSkillRuns",
                columns: new[] { "WorkspaceId", "SkillName", "ProjectName" },
                unique: true);
        }
    }
}
