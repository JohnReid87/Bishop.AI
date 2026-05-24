using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bishop.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "Cards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    BranchName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    BaseBranch = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ClosedReason = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    WorktreePath = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Batches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cards_BatchId",
                table: "Cards",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Batches_BranchName",
                table: "Batches",
                column: "BranchName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_Batches_BatchId",
                table: "Cards",
                column: "BatchId",
                principalTable: "Batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cards_Batches_BatchId",
                table: "Cards");

            migrationBuilder.DropTable(
                name: "Batches");

            migrationBuilder.DropIndex(
                name: "IX_Cards_BatchId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "Cards");
        }
    }
}
