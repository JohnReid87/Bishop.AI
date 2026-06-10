using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bishop.Data.Migrations
{
    /// <inheritdoc />
    public partial class PartialBranchNameIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Batches_BranchName",
                table: "Batches");

            migrationBuilder.CreateIndex(
                name: "IX_Batches_BranchName",
                table: "Batches",
                column: "BranchName",
                unique: true,
                filter: "Status != 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Batches_BranchName",
                table: "Batches");

            migrationBuilder.CreateIndex(
                name: "IX_Batches_BranchName",
                table: "Batches",
                column: "BranchName",
                unique: true);
        }
    }
}
