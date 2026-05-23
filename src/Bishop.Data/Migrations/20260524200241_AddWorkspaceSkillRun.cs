using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bishop.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceSkillRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkspaceSkillRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    WorkspaceId = table.Column<Guid>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    SkillName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    GitSha = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceSkillRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkspaceSkillRuns_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceSkillRuns_WorkspaceId_SkillName",
                table: "WorkspaceSkillRuns",
                columns: new[] { "WorkspaceId", "SkillName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkspaceSkillRuns");
        }
    }
}
