using Bishop.Life.Core;
using Bishop.Life.Core.Schema;
using FluentAssertions;

namespace Bishop.Life.Tests;

public class LifePlanWatcherTests
{
    [Fact]
    public void Debounces_BurstyChanges_IntoSingleReloadedEvent()
    {
        using var tmp = new TempDir();
        var filePath = tmp.FilePath();
        File.WriteAllText(filePath, "{}");

        using var watcher = new LifePlanWatcher(filePath, TimeSpan.FromMilliseconds(150));
        var fired = 0;
        watcher.Reloaded += (_, _) => Interlocked.Increment(ref fired);
        watcher.Start();

        for (var i = 0; i < 5; i++)
        {
            File.WriteAllText(filePath, $"{{\"i\":{i}}}");
            Thread.Sleep(20);
        }

        Thread.Sleep(600);

        fired.Should().Be(1);
    }

    [Fact]
    public void FiresOnce_PerSaveCycle_ViaFileService()
    {
        using var tmp = new TempDir();
        var filePath = tmp.FilePath();
        var service = new LifePlanFileService(filePath);
        service.Save(new LifePlan { Meta = new Meta { CreatedAt = DateTimeOffset.UtcNow } });

        using var watcher = new LifePlanWatcher(filePath, TimeSpan.FromMilliseconds(150));
        var fired = 0;
        watcher.Reloaded += (_, _) => Interlocked.Increment(ref fired);
        watcher.Start();

        Thread.Sleep(100);
        service.Save(new LifePlan { Meta = new Meta { CreatedAt = DateTimeOffset.UtcNow.AddMinutes(1) } });

        Thread.Sleep(600);

        fired.Should().Be(1);
    }

    [Fact]
    public void StopsRaising_AfterDispose()
    {
        using var tmp = new TempDir();
        var filePath = tmp.FilePath();
        File.WriteAllText(filePath, "{}");

        var watcher = new LifePlanWatcher(filePath, TimeSpan.FromMilliseconds(100));
        var fired = 0;
        watcher.Reloaded += (_, _) => Interlocked.Increment(ref fired);
        watcher.Start();
        watcher.Dispose();

        File.WriteAllText(filePath, "{\"x\":1}");
        Thread.Sleep(400);

        fired.Should().Be(0);
    }
}
