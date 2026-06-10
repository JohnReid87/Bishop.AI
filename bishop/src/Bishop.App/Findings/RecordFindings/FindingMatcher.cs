using Bishop.Data;
using CoreEntities = Bishop.Core;

namespace Bishop.App.Findings.RecordFindings;

internal static class FindingMatcher
{
    // (File, Title) → all existing rows in this run that share that key.
    // Built case-sensitively to match how the new IdentityHash hashes its inputs.
    internal static Dictionary<(string File, string Title), List<CoreEntities.Finding>> BuildFileTitleLookup(
        IEnumerable<CoreEntities.Finding> findings)
    {
        var map = new Dictionary<(string, string), List<CoreEntities.Finding>>();
        foreach (var f in findings)
        {
            if (string.IsNullOrEmpty(f.File))
                continue;
            var key = (f.File, f.Title);
            if (!map.TryGetValue(key, out var bucket))
            {
                bucket = new List<CoreEntities.Finding>();
                map[key] = bucket;
            }
            bucket.Add(f);
        }
        return map;
    }

    internal static List<CoreEntities.Finding> CollectMatches(
        Dictionary<string, CoreEntities.Finding> byHash,
        Dictionary<(string File, string Title), List<CoreEntities.Finding>> byFileTitle,
        string hash,
        string? file,
        string title)
    {
        var matches = new List<CoreEntities.Finding>();
        if (byHash.TryGetValue(hash, out var hashMatch))
            matches.Add(hashMatch);

        if (!string.IsNullOrEmpty(file)
            && byFileTitle.TryGetValue((file, title), out var fileTitleMatches))
        {
            foreach (var m in fileTitleMatches)
            {
                if (!matches.Contains(m))
                    matches.Add(m);
            }
        }
        return matches;
    }

    // Status priority: carded > dismissed > parked > pending > resolved.
    // Returns the winner and deletes the rest, carrying LinkedCardId/RebuttalText forward.
    internal static CoreEntities.Finding MergeDuplicates(
        BishopDbContext db,
        List<CoreEntities.Finding> matches)
    {
        if (matches.Count == 1)
            return matches[0];

        matches.Sort((a, b) => StatusPriority(b.Status).CompareTo(StatusPriority(a.Status)));
        var winner = matches[0];
        for (var i = 1; i < matches.Count; i++)
        {
            var loser = matches[i];
            winner.LinkedCardId ??= loser.LinkedCardId;
            winner.RebuttalText ??= loser.RebuttalText;
            db.Findings.Remove(loser);
        }
        return winner;
    }

    private static int StatusPriority(string status) => status switch
    {
        "carded" => 4,
        "dismissed" => 3,
        "parked" => 2,
        "pending" => 1,
        _ => 0, // resolved or anything unrecognised
    };
}
