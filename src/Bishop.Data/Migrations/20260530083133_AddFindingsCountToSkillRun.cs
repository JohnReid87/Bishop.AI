using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bishop.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFindingsCountToSkillRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FindingsCount",
                table: "WorkspaceSkillRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FindingsCount",
                table: "WorkspaceSkillRuns");
        }
    }
}
