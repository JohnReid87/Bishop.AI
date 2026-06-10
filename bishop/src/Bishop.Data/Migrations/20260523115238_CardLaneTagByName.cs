using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bishop.Data.Migrations
{
    /// <inheritdoc />
    public partial class CardLaneTagByName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the new columns with safe defaults so SQLite's NOT NULL constraint
            // is satisfied for existing rows; backfill replaces the defaults below.
            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "Cards",
                type: "TEXT",
                nullable: false,
                defaultValue: Guid.Empty,
                collation: "NOCASE");

            migrationBuilder.AddColumn<string>(
                name: "LaneName",
                table: "Cards",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TagName",
                table: "Cards",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            // Backfill from the existing FK joins before the FK columns are dropped.
            migrationBuilder.Sql(
                """
                UPDATE "Cards"
                SET "WorkspaceId" = (SELECT "WorkspaceId" FROM "Lanes" WHERE "Lanes"."Id" = "Cards"."LaneId"),
                    "LaneName"    = (SELECT "Name"        FROM "Lanes" WHERE "Lanes"."Id" = "Cards"."LaneId");
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Cards"
                SET "TagName" = (SELECT "Name" FROM "Tags" WHERE "Tags"."Id" = "Cards"."TagId")
                WHERE "TagId" IS NOT NULL;
                """);

            // Drop the now-redundant FK columns. The FK constraints / indexes go first.
            migrationBuilder.DropForeignKey(
                name: "FK_Cards_Lanes_LaneId",
                table: "Cards");

            migrationBuilder.DropForeignKey(
                name: "FK_Cards_Tags_TagId",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_LaneId",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_TagId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "LaneId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "TagId",
                table: "Cards");

            // Wire up the new Workspace FK + index.
            migrationBuilder.CreateIndex(
                name: "IX_Cards_WorkspaceId",
                table: "Cards",
                column: "WorkspaceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_Workspaces_WorkspaceId",
                table: "Cards",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cards_Workspaces_WorkspaceId",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_WorkspaceId",
                table: "Cards");

            migrationBuilder.AddColumn<Guid>(
                name: "LaneId",
                table: "Cards",
                type: "TEXT",
                nullable: false,
                defaultValue: Guid.Empty,
                collation: "NOCASE");

            migrationBuilder.AddColumn<Guid>(
                name: "TagId",
                table: "Cards",
                type: "TEXT",
                nullable: true,
                collation: "NOCASE");

            // Best-effort re-derive: match by (WorkspaceId, Name). May leave LaneId zero
            // if the named lane has since been deleted.
            migrationBuilder.Sql(
                """
                UPDATE "Cards"
                SET "LaneId" = COALESCE(
                    (SELECT "Id" FROM "Lanes"
                     WHERE "Lanes"."WorkspaceId" = "Cards"."WorkspaceId"
                       AND "Lanes"."Name"        = "Cards"."LaneName"),
                    "LaneId");
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Cards"
                SET "TagId" = (SELECT "Id" FROM "Tags"
                               WHERE "Tags"."WorkspaceId" = "Cards"."WorkspaceId"
                                 AND "Tags"."Name"        = "Cards"."TagName")
                WHERE "TagName" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "LaneName",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "TagName",
                table: "Cards");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_LaneId",
                table: "Cards",
                column: "LaneId");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_TagId",
                table: "Cards",
                column: "TagId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_Lanes_LaneId",
                table: "Cards",
                column: "LaneId",
                principalTable: "Lanes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_Tags_TagId",
                table: "Cards",
                column: "TagId",
                principalTable: "Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
