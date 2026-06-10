using Bishop.Life.Core;
using Bishop.Life.Core.Schema;
using FluentAssertions;

namespace Bishop.Life.Tests;

public class LifePlanFileServiceTests
{
    [Fact]
    public void Save_Then_Load_RoundTrips()
    {
        using var tmp = new TempDir();
        var service = new LifePlanFileService(tmp.FilePath());

        var plan = new LifePlan
        {
            Meta = new Meta { CreatedAt = DateTimeOffset.UtcNow },
            Areas = { new Area { Id = "a", Name = "A", Color = "#111111" } },
        };

        service.Save(plan);
        var loaded = service.Load();

        loaded.Should().BeEquivalentTo(plan);
    }

    [Fact]
    public void Save_FirstTime_DoesNotCreatePrev()
    {
        using var tmp = new TempDir();
        var service = new LifePlanFileService(tmp.FilePath());

        service.Save(new LifePlan { Meta = new Meta { CreatedAt = DateTimeOffset.UtcNow } });

        File.Exists(service.PrevPath).Should().BeFalse();
        File.Exists(service.FilePath).Should().BeTrue();
    }

    [Fact]
    public void Save_SecondTime_SnapshotsPreviousToPrev()
    {
        using var tmp = new TempDir();
        var service = new LifePlanFileService(tmp.FilePath());

        var first = new LifePlan
        {
            Meta = new Meta { CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) },
        };
        service.Save(first);
        var firstJson = File.ReadAllText(service.FilePath);

        var second = new LifePlan
        {
            Meta = new Meta { CreatedAt = new DateTimeOffset(2026, 2, 2, 0, 0, 0, TimeSpan.Zero) },
        };
        service.Save(second);

        File.Exists(service.PrevPath).Should().BeTrue();
        File.ReadAllText(service.PrevPath).Should().Be(firstJson);
    }

    [Fact]
    public void Save_LeavesNoTempFileOnSuccess()
    {
        using var tmp = new TempDir();
        var service = new LifePlanFileService(tmp.FilePath());

        service.Save(new LifePlan());
        service.Save(new LifePlan { Meta = new Meta { CreatedAt = DateTimeOffset.UtcNow } });

        File.Exists(service.TempPath).Should().BeFalse();
    }

    [Fact]
    public void Save_PreExistingPartialTemp_IsOverwritten()
    {
        using var tmp = new TempDir();
        var service = new LifePlanFileService(tmp.FilePath());

        Directory.CreateDirectory(Path.GetDirectoryName(service.FilePath)!);
        File.WriteAllText(service.TempPath, "stale-partial-content");

        service.Save(new LifePlan { Meta = new Meta { CreatedAt = DateTimeOffset.UtcNow } });

        File.Exists(service.TempPath).Should().BeFalse();
        File.Exists(service.FilePath).Should().BeTrue();
        service.Load().Should().NotBeNull();
    }

    [Fact]
    public void Save_CreatesDirectoryIfMissing()
    {
        using var tmp = new TempDir();
        var nested = Path.Combine(tmp.Path, "deep", "deeper");
        var filePath = Path.Combine(nested, "bishop.life.json");
        var service = new LifePlanFileService(filePath);

        service.Save(new LifePlan { Meta = new Meta { CreatedAt = DateTimeOffset.UtcNow } });

        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public void Exists_ReflectsFilePresence()
    {
        using var tmp = new TempDir();
        var service = new LifePlanFileService(tmp.FilePath());

        service.Exists().Should().BeFalse();
        service.Save(new LifePlan());
        service.Exists().Should().BeTrue();
    }
}
