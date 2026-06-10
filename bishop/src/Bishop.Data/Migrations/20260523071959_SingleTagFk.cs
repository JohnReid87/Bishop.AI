using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bishop.Data.Migrations
{
    /// <inheritdoc />
    public partial class SingleTagFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TagId",
                table: "Cards",
                type: "TEXT",
                nullable: true,
                collation: "NOCASE");

            // Backfill: pick the first CardTag row per card (by rowid) to preserve
            // the tag that was shown on the board before this migration.
            migrationBuilder.Sql(
                "UPDATE Cards SET TagId = (SELECT TagId FROM CardTags WHERE CardId = Cards.Id ORDER BY rowid LIMIT 1)");

            migrationBuilder.DropTable(
                name: "CardTags");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_TagId",
                table: "Cards",
                column: "TagId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_Tags_TagId",
                table: "Cards",
                column: "TagId",
                principalTable: "Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cards_Tags_TagId",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_TagId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "TagId",
                table: "Cards");

            migrationBuilder.CreateTable(
                name: "CardTags",
                columns: table => new
                {
                    CardId = table.Column<Guid>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    TagId = table.Column<Guid>(type: "TEXT", nullable: false, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardTags", x => new { x.CardId, x.TagId });
                    table.ForeignKey(
                        name: "FK_CardTags_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CardTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardTags_TagId",
                table: "CardTags",
                column: "TagId");
        }
    }
}
