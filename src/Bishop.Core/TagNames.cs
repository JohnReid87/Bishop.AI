namespace Bishop.Core;

public static class TagNames
{
    public const string Feature = "feature";
    public const string Bug = "bug";
    public const string Chore = "chore";
    public const string Docs = "docs";
    public const string Arch = "arch";
    public const string Test = "test";
    public const string Spike = "spike";
    public const string Security = "security";

    public static readonly IReadOnlyList<string> All = [Feature, Bug, Chore, Docs, Arch, Test, Spike, Security];
}
