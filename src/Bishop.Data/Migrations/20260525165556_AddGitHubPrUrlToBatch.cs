using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bishop.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubPrUrlToBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GitHubPrUrl",
                table: "Batches",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GitHubPrUrl",
                table: "Batches");
        }
    }
}
