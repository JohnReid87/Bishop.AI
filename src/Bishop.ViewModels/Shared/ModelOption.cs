namespace Bishop.ViewModels.Shared;

public sealed record ModelOption(string Id, string Label)
{
    public override string ToString() => Label;
}
