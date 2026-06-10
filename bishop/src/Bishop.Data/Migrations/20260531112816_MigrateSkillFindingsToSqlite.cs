using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bishop.Data.Migrations
{
    /// <inheritdoc />
    public partial class MigrateSkillFindingsToSqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkspaceSkillRuns_WorkspaceId_SkillName",
                table: "WorkspaceSkillRuns");

            migrationBuilder.AddColumn<string>(
                name: "ProjectName",
                table: "WorkspaceSkillRuns",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Findings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    WorkspaceSkillRunId = table.Column<Guid>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    IdentityHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ProjectName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    File = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Rule = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RebuttalText = table.Column<string>(type: "TEXT", nullable: true),
                    LinkedCardId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Findings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Findings_WorkspaceSkillRuns_WorkspaceSkillRunId",
                        column: x => x.WorkspaceSkillRunId,
                        principalTable: "WorkspaceSkillRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceSkillRuns_WorkspaceId_SkillName_ProjectName",
                table: "WorkspaceSkillRuns",
                columns: new[] { "WorkspaceId", "SkillName", "ProjectName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Findings_WorkspaceSkillRunId_IdentityHash",
                table: "Findings",
                columns: new[] { "WorkspaceSkillRunId", "IdentityHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Findings");

            migrationBuilder.DropIndex(
                name: "IX_WorkspaceSkillRuns_WorkspaceId_SkillName_ProjectName",
                table: "WorkspaceSkillRuns");

            migrationBuilder.DropColumn(
                name: "ProjectName",
                table: "WorkspaceSkillRuns");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceSkillRuns_WorkspaceId_SkillName",
                table: "WorkspaceSkillRuns",
                columns: new[] { "WorkspaceId", "SkillName" },
                unique: true);
        }
    }
}
