using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bishop.Data.Migrations
{
    /// <inheritdoc />
    public partial class DedupeFindingsByFileTitle : Migration
    {
        // Pre-#959 the IdentityHash included LLM-emitted Rule/Symbol, which drift between runs.
        // That left multiple Findings rows per logical finding within a single WorkspaceSkillRun.
        // Collapse those duplicates by (RunId, File, Title), keeping the highest-priority Status
        // (carded > dismissed > parked > pending > resolved) and merging LinkedCardId / RebuttalText
        // forward. Remaining stale IdentityHash values are healed by the handler on the next run.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TEMPORARY TABLE __finding_winners AS
SELECT Id FROM (
    SELECT Id,
           ROW_NUMBER() OVER (
               PARTITION BY WorkspaceSkillRunId, File, Title
               ORDER BY CASE Status
                          WHEN 'carded' THEN 4
                          WHEN 'dismissed' THEN 3
                          WHEN 'parked' THEN 2
                          WHEN 'pending' THEN 1
                          ELSE 0 END DESC,
                        FirstSeenAt ASC,
                        Id ASC
           ) AS rn
    FROM Findings
    WHERE File IS NOT NULL AND File <> ''
) WHERE rn = 1;
");

            migrationBuilder.Sql(@"
UPDATE Findings AS w
SET LinkedCardId = (
    SELECT l.LinkedCardId FROM Findings l
    WHERE l.WorkspaceSkillRunId = w.WorkspaceSkillRunId
      AND l.File = w.File AND l.Title = w.Title
      AND l.Id <> w.Id
      AND l.LinkedCardId IS NOT NULL
    LIMIT 1
)
WHERE w.Id IN (SELECT Id FROM __finding_winners)
  AND w.LinkedCardId IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE Findings AS w
SET RebuttalText = (
    SELECT l.RebuttalText FROM Findings l
    WHERE l.WorkspaceSkillRunId = w.WorkspaceSkillRunId
      AND l.File = w.File AND l.Title = w.Title
      AND l.Id <> w.Id
      AND l.RebuttalText IS NOT NULL
    LIMIT 1
)
WHERE w.Id IN (SELECT Id FROM __finding_winners)
  AND w.RebuttalText IS NULL;
");

            migrationBuilder.Sql(@"
DELETE FROM Findings
WHERE Id IN (
    SELECT f.Id FROM Findings f
    INNER JOIN (
        SELECT WorkspaceSkillRunId, File, Title
        FROM Findings
        WHERE File IS NOT NULL AND File <> ''
        GROUP BY WorkspaceSkillRunId, File, Title
        HAVING COUNT(*) > 1
    ) dup
      ON f.WorkspaceSkillRunId = dup.WorkspaceSkillRunId
     AND f.File = dup.File
     AND f.Title = dup.Title
    WHERE f.Id NOT IN (SELECT Id FROM __finding_winners)
);
");

            migrationBuilder.Sql("DROP TABLE __finding_winners;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dedupe is not reversible — deleted loser rows cannot be reconstructed.
        }
    }
}
