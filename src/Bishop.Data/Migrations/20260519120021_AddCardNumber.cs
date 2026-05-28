using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bishop.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCardNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NextCardNumber",
                table: "Workspaces",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "Cards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Backfill: assign sequential per-workspace numbers ordered by CreatedAt ascending.
            // Uses correlated COUNT(*) to simulate ROW_NUMBER without requiring a CTE,
            // which keeps this compatible with the SQLite version bundled by EF Core.
            migrationBuilder.Sql(@"
UPDATE Cards
SET Number = (
    SELECT COUNT(*)
    FROM Cards AS c2
    JOIN Lanes AS l1 ON c2.LaneId = l1.Id
    JOIN Lanes AS l2 ON Cards.LaneId = l2.Id
    WHERE l1.WorkspaceId = l2.WorkspaceId
      AND (c2.CreatedAt < Cards.CreatedAt
           OR (c2.CreatedAt = Cards.CreatedAt AND c2.Id <= Cards.Id))
);

UPDATE Workspaces
SET NextCardNumber = COALESCE(
    (SELECT MAX(c.Number) + 1
     FROM Cards c
     JOIN Lanes l ON c.LaneId = l.Id
     WHERE l.WorkspaceId = Workspaces.Id),
    1
);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextCardNumber",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "Cards");
        }
    }
}
