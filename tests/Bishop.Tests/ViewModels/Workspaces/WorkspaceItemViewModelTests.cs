using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Errors;
using Bishop.ViewModels.Scripts;
using Bishop.ViewModels.Settings;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Skills;
using Bishop.ViewModels.Workspaces;
using FluentAssertions;

namespace Bishop.Tests.ViewModels.Workspaces;

public class WorkspaceItemViewModelTests
{
    [Fact]
    public void FirstLetter_UppercaseInitial()
    {
        new WorkspaceItemViewModel { Name = "bishop" }.FirstLetter.Should().Be("B");
        new WorkspaceItemViewModel { Name = "Acme" }.FirstLetter.Should().Be("A");
    }

    [Fact]
    public void FirstLetter_QuestionMarkWhenNameEmpty()
    {
        new WorkspaceItemViewModel { Name = string.Empty }.FirstLetter.Should().Be("?");
    }

}
