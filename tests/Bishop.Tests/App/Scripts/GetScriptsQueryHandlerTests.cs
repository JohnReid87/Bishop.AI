using Bishop.App.Scripts.GetScripts;
using FluentAssertions;

namespace Bishop.Tests.App.Scripts;

public sealed class GetScriptsQueryHandlerTests : IDisposable
{
    private readonly string _scriptsFolder;

    public GetScriptsQueryHandlerTests()
    {
        _scriptsFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_scriptsFolder);
    }

    public void Dispose()
    {
        Directory.Delete(_scriptsFolder, recursive: true);
    }

    private GetScriptsQueryHandler CreateSut() => new(_scriptsFolder);

    [Fact]
    public async Task Handle_FolderIsEmpty_ReturnsEmptyList()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetScriptsQuery(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_FolderHasPs1Files_ReturnsMatchingScripts()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_scriptsFolder, "deploy.ps1"), "# deploy");
        File.WriteAllText(Path.Combine(_scriptsFolder, "build.ps1"), "# build");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetScriptsQuery(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Select(s => s.Name).Should().BeEquivalentTo(["build", "deploy"]);
    }

    [Fact]
    public async Task Handle_ReturnsScriptsSortedByName()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_scriptsFolder, "zebra.ps1"), "");
        File.WriteAllText(Path.Combine(_scriptsFolder, "alpha.ps1"), "");
        File.WriteAllText(Path.Combine(_scriptsFolder, "mango.ps1"), "");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetScriptsQuery(), CancellationToken.None);

        // Assert
        result.Select(s => s.Name).Should().ContainInOrder("alpha", "mango", "zebra");
    }

    [Fact]
    public async Task Handle_FolderHasNonPs1Files_ExcludesNonPs1Files()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_scriptsFolder, "script.ps1"), "");
        File.WriteAllText(Path.Combine(_scriptsFolder, "readme.txt"), "");
        File.WriteAllText(Path.Combine(_scriptsFolder, "tool.sh"), "");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetScriptsQuery(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("script");
    }

    [Fact]
    public async Task Handle_ScriptInfoHasCorrectPathAndName()
    {
        // Arrange
        var scriptPath = Path.Combine(_scriptsFolder, "my-script.ps1");
        File.WriteAllText(scriptPath, "");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetScriptsQuery(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("my-script");
        result[0].Path.Should().Be(scriptPath);
    }

    [Fact]
    public async Task Handle_FolderDoesNotExist_CreatesItAndReturnsEmpty()
    {
        // Arrange
        var newFolder = Path.Combine(Path.GetTempPath(), "bishop-scripts-test-" + Guid.NewGuid());
        var sut = new GetScriptsQueryHandler(newFolder);

        try
        {
            // Act
            var result = await sut.Handle(new GetScriptsQuery(), CancellationToken.None);

            // Assert
            result.Should().BeEmpty();
            Directory.Exists(newFolder).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(newFolder))
                Directory.Delete(newFolder, recursive: true);
        }
    }

    [Fact]
    public async Task ParameterlessConstructor_UsesExpectedAppDataPath()
    {
        var expectedFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Bishop.AI", "scripts");

        var sut = new GetScriptsQueryHandler();

        var field = typeof(GetScriptsQueryHandler)
            .GetField("_scriptsFolder", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var resolvedPath = (string)field!.GetValue(sut)!;
        resolvedPath.Should().Be(expectedFolder);
    }
}
