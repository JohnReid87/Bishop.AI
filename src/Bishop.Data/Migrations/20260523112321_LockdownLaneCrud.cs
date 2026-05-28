using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bishop.Data.Migrations
{
    /// <inheritdoc />
    public partial class LockdownLaneCrud : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Promote any pre-existing "Backlog" lane to system. The earlier
            // AddLaneIsSystem migration only marked To Do / Doing / Done.
            migrationBuilder.Sql(
                "UPDATE \"Lanes\" SET \"IsSystem\" = 1 WHERE LOWER(\"Name\") = 'backlog';");

            // For any workspace missing a Backlog, shift other lanes up by 1
            // and insert a new Backlog at position 1.
            migrationBuilder.Sql(
                """
                UPDATE "Lanes"
                SET "Position" = "Position" + 1
                WHERE "WorkspaceId" IN (
                    SELECT w."Id" FROM "Workspaces" w
                    WHERE NOT EXISTS (
                        SELECT 1 FROM "Lanes" l
                        WHERE l."WorkspaceId" = w."Id"
                          AND LOWER(l."Name") = 'backlog'
                    )
                );
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "Lanes" ("Id", "WorkspaceId", "Name", "Position", "IsSystem")
                SELECT
                    LOWER(
                        SUBSTR(HEX(RANDOMBLOB(4)), 1, 8) || '-' ||
                        SUBSTR(HEX(RANDOMBLOB(2)), 1, 4) || '-' ||
                        SUBSTR(HEX(RANDOMBLOB(2)), 1, 4) || '-' ||
                        SUBSTR(HEX(RANDOMBLOB(2)), 1, 4) || '-' ||
                        SUBSTR(HEX(RANDOMBLOB(6)), 1, 12)
                    ),
                    w."Id",
                    'Backlog',
                    1,
                    1
                FROM "Workspaces" w
                WHERE NOT EXISTS (
                    SELECT 1 FROM "Lanes" l
                    WHERE l."WorkspaceId" = w."Id"
                      AND LOWER(l."Name") = 'backlog'
                );
                """);

            // Move every card out of any non-system lane into its workspace's
            // Backlog, appended after Backlog's existing cards. Source order is
            // preserved (source lane Position, then card Position).
            //
            // MATERIALIZED is load-bearing: SQLite otherwise re-evaluates the
            // subqueries per row, so each successive move sees the previous
            // card's new position and ranks drift.
            migrationBuilder.Sql(
                """
                WITH new_assignments AS MATERIALIZED (
                    SELECT
                        c."Id" AS card_id,
                        bl."Id" AS new_lane_id,
                        maxes.bl_max + ROW_NUMBER() OVER (
                            PARTITION BY l."WorkspaceId"
                            ORDER BY l."Position", c."Position", c."Id"
                        ) AS new_position
                    FROM "Cards" c
                    INNER JOIN "Lanes" l ON l."Id" = c."LaneId" AND l."IsSystem" = 0
                    INNER JOIN "Lanes" bl ON bl."WorkspaceId" = l."WorkspaceId"
                                         AND LOWER(bl."Name") = 'backlog'
                    INNER JOIN (
                        SELECT b."Id" AS bl_id,
                               COALESCE(MAX(c2."Position"), 0) AS bl_max
                        FROM "Lanes" b
                        LEFT JOIN "Cards" c2 ON c2."LaneId" = b."Id"
                        WHERE LOWER(b."Name") = 'backlog'
                        GROUP BY b."Id"
                    ) maxes ON maxes.bl_id = bl."Id"
                )
                UPDATE "Cards"
                SET
                    "LaneId" = (SELECT new_lane_id FROM new_assignments WHERE card_id = "Cards"."Id"),
                    "Position" = (SELECT new_position FROM new_assignments WHERE card_id = "Cards"."Id")
                WHERE "Id" IN (SELECT card_id FROM new_assignments);
                """);

            // Delete the (now empty) non-system lanes.
            migrationBuilder.Sql(
                "DELETE FROM \"Lanes\" WHERE \"IsSystem\" = 0;");

            // Renumber lane positions per workspace so they remain contiguous.
            // MATERIALIZED so the ROW_NUMBER ranks aren't recomputed against
            // already-updated rows.
            migrationBuilder.Sql(
                """
                WITH ranked AS MATERIALIZED (
                    SELECT "Id",
                           ROW_NUMBER() OVER (PARTITION BY "WorkspaceId" ORDER BY "Position", "Id") AS new_pos
                    FROM "Lanes"
                )
                UPDATE "Lanes"
                SET "Position" = (SELECT new_pos FROM ranked WHERE ranked."Id" = "Lanes"."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data migration only — schema is unchanged. Down is a no-op:
            // we cannot resurrect deleted non-system lanes or their original
            // card placements.
        }
    }
}
