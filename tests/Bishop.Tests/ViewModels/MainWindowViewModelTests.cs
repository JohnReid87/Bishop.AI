using Bishop.App.CatMode;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Core;
using Bishop.ViewModels;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Bishop.Tests.ViewModels;

public class MainWindowViewModelTests
{
    [Fact]
    public void IsWorkspaceListEmpty_TrueOnConstruction()
    {
        var vm = NewVm();

        vm.IsWorkspaceListEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsContentEmpty_TrueWhenNoSelection()
    {
        var vm = NewVm();

        vm.IsContentEmpty.Should().BeTrue();

        vm.SelectedWorkspace = new WorkspaceItemViewModel { Id = Guid.NewGuid(), Name = "ws" };
        vm.IsContentEmpty.Should().BeFalse();
    }

    [Fact]
    public void IsCatModeActive_ReflectsCatModeService()
    {
        var catMode = new CatModeService();
        var vm = NewVm(catMode: catMode);

        vm.IsCatModeActive.Should().BeFalse();

        catMode.Toggle();

        vm.IsCatModeActive.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_PopulatesWorkspacesFromMediator()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Workspace>
            {
                new() { Id = Guid.NewGuid(), Name = "alpha", Path = "C:/a", Position = 1 },
                new() { Id = Guid.NewGuid(), Name = "beta", Path = "C:/b", Position = 2 },
            });

        var vm = NewVm(mediator: mediator);

        await vm.LoadAsync();

        vm.Workspaces.Should().HaveCount(2);
        vm.IsWorkspaceListEmpty.Should().BeFalse();
    }

    private static MainWindowViewModel NewVm(IMediator? mediator = null, ICatModeService? catMode = null) =>
        new(mediator ?? Substitute.For<IMediator>(), catMode ?? new CatModeService());
}
