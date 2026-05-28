namespace Bishop.App.Findings.RecordFindings;

public sealed record RecordFindingsResult(string JsonPath, string HtmlPath, int FindingCount);
