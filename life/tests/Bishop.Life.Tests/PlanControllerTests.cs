using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bishop.Life.App.Plan;
using Bishop.Life.Core;
using Bishop.Life.Core.Schema;
using Bishop.Life.Core.Schema.Envelopes;
using Bishop.Life.Core.Web;
using FluentAssertions;

namespace Bishop.Life.Tests;

/// <summary>
/// Envelope-sequencing coverage for the plan slice extracted in card #1071.
/// All tests run synchronously by wiring <c>uiPost</c> to invoke inline and
/// driving the watcher-reload path through <see cref="PlanController.OnFileReloaded"/>
/// rather than mutating the real file system watcher.
/// </summary>
public class PlanControllerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public PlanControllerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bishop.life.tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "bishop.life.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"PlanControllerTests: temp cleanup failed: {ex.Message}"); }
    }

    [Fact]
    public void NotifyNavigated_WithFileMissing_PostsMissingEnvelope()
    {
        using var harness = new Harness(_filePath);

        harness.Controller.NotifyNavigated();

        var envelope = harness.SingleEnvelope();
        envelope.Status.Should().Be("missing");
        envelope.Plan.Should().BeNull();
        envelope.FilePath.Should().Be(_filePath);
        envelope.StandupInFlight.Should().BeFalse();
        envelope.AddInFlight.Should().BeFalse();
    }

    [Fact]
    public void NotifyNavigated_WithFilePresent_PostsOkEnvelopeWithPlan()
    {
        WriteSeedPlan();
        using var harness = new Harness(_filePath);

        harness.Controller.NotifyNavigated();

        var envelope = harness.SingleEnvelope();
        envelope.Status.Should().Be("ok");
        envelope.Plan.Should().NotBeNull();
    }

    [Fact]
    public void NotifyNavigated_WhenLoadThrows_PostsErrorEnvelope()
    {
        File.WriteAllText(_filePath, "{ not valid json");
        using var harness = new Harness(_filePath);

        harness.Controller.NotifyNavigated();

        var envelope = harness.SingleEnvelope();
        envelope.Status.Should().Be("error");
        envelope.Plan.Should().BeNull();
        envelope.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ApplyMutation_BeforeNavigated_AppliesToDiskButDoesNotPost()
    {
        using var harness = new Harness(_filePath);
        var planJson = SerializeNewPlan();

        harness.Controller.ApplyMutation(ParsePlanElement(planJson));

        File.Exists(_filePath).Should().BeTrue();
        harness.Channel.Posts.Should().BeEmpty();
    }

    [Fact]
    public void ApplyMutation_AfterNavigated_WritesPlan_AndPostsOkEnvelope()
    {
        using var harness = new Harness(_filePath);
        harness.Controller.NotifyNavigated();
        harness.Channel.Posts.Clear();

        harness.Controller.ApplyMutation(ParsePlanElement(SerializeNewPlan()));

        File.Exists(_filePath).Should().BeTrue();
        // ApplyMutation calls _coordinator.ApplyMutation (no StateChanged) then PostState.
        harness.Channel.Posts.Should().ContainSingle();
        harness.SingleEnvelope().Status.Should().Be("ok");
    }

    [Fact]
    public void ApplyMutation_WithNullPlanLiteral_DropsSilently()
    {
        using var harness = new Harness(_filePath);
        harness.Controller.NotifyNavigated();
        harness.Channel.Posts.Clear();

        using var doc = JsonDocument.Parse("null");
        harness.Controller.ApplyMutation(doc.RootElement);

        File.Exists(_filePath).Should().BeFalse();
        harness.Channel.Posts.Should().BeEmpty();
    }

    [Fact]
    public void OnFileReloaded_WithStandupInFlight_ClearsFlag_AndPostsEnvelope()
    {
        using var harness = new Harness(_filePath);
        harness.Controller.NotifyNavigated();
        harness.Coordinator.NoteStandupLaunched(); // fires StateChanged → 1 post
        harness.Channel.Posts.Clear();

        harness.Controller.OnFileReloaded();

        harness.Coordinator.StandupInFlight.Should().BeFalse();
        // NoteStandupAborted fires StateChanged → a single PostState via the subscription.
        harness.Channel.Posts.Should().ContainSingle();
        harness.SingleEnvelope().StandupInFlight.Should().BeFalse();
    }

    [Fact]
    public void OnFileReloaded_WithAddInFlight_ClearsFlag()
    {
        using var harness = new Harness(_filePath);
        harness.Controller.NotifyNavigated();
        harness.Coordinator.NoteAddLaunched();
        harness.Channel.Posts.Clear();

        harness.Controller.OnFileReloaded();

        harness.Coordinator.AddInFlight.Should().BeFalse();
    }

    [Fact]
    public void OnFileReloaded_WithNoFlagsSet_PostsCurrentState()
    {
        using var harness = new Harness(_filePath);
        harness.Controller.NotifyNavigated();
        harness.Channel.Posts.Clear();

        harness.Controller.OnFileReloaded();

        harness.Channel.Posts.Should().ContainSingle();
        harness.SingleEnvelope().Status.Should().Be("missing");
    }

    [Fact]
    public void NoteStandupSessionEnded_WithStandupInFlight_ClearsFlag()
    {
        using var harness = new Harness(_filePath);
        harness.Controller.NotifyNavigated();
        harness.Coordinator.NoteStandupLaunched();
        harness.Channel.Posts.Clear();

        harness.Controller.NoteStandupSessionEnded();

        harness.Coordinator.StandupInFlight.Should().BeFalse();
        harness.Channel.Posts.Should().ContainSingle();
        harness.SingleEnvelope().StandupInFlight.Should().BeFalse();
    }

    [Fact]
    public void NoteStandupSessionEnded_WithNoFlag_PostsState()
    {
        using var harness = new Harness(_filePath);
        harness.Controller.NotifyNavigated();
        harness.Channel.Posts.Clear();

        harness.Controller.NoteStandupSessionEnded();

        harness.Channel.Posts.Should().ContainSingle();
    }

    [Fact]
    public void NoteWindowActivated_BeforeNavigated_IsNoop()
    {
        using var harness = new Harness(_filePath);

        harness.Controller.NoteWindowActivated();

        harness.Channel.Posts.Should().BeEmpty();
    }

    [Fact]
    public void NoteWindowActivated_ClearsBothInFlightFlags()
    {
        using var harness = new Harness(_filePath);
        harness.Controller.NotifyNavigated();
        harness.Coordinator.NoteStandupLaunched();
        harness.Coordinator.NoteAddLaunched();
        harness.Channel.Posts.Clear();

        harness.Controller.NoteWindowActivated();

        harness.Coordinator.StandupInFlight.Should().BeFalse();
        harness.Coordinator.AddInFlight.Should().BeFalse();
    }

    [Fact]
    public void CoordinatorStateChanged_FromAnyNoteCall_TriggersEnvelopePost()
    {
        using var harness = new Harness(_filePath);
        harness.Controller.NotifyNavigated();
        harness.Channel.Posts.Clear();

        harness.Coordinator.NoteAddLaunched();

        harness.Channel.Posts.Should().ContainSingle();
        harness.SingleEnvelope().AddInFlight.Should().BeTrue();
    }

    [Fact]
    public void Dispose_UnsubscribesFromCoordinatorAndStopsPosting()
    {
        using var harness = new Harness(_filePath);
        harness.Controller.NotifyNavigated();
        harness.Channel.Posts.Clear();

        harness.Controller.Dispose();
        harness.Coordinator.NoteAddLaunched();

        harness.Channel.Posts.Should().BeEmpty();
    }

    private void WriteSeedPlan() => File.WriteAllText(_filePath, SerializeNewPlan());

    private static string SerializeNewPlan() => LifePlanJson.Serialize(new LifePlan());

    private static JsonElement ParsePlanElement(string planJson)
    {
        var doc = JsonDocument.Parse(planJson);
        return doc.RootElement.Clone();
    }

    private sealed class Harness : IDisposable
    {
        public FakeBrowserChannel Channel { get; } = new();
        public LifePlanFileService Service { get; }
        public LifePlanWatcher Watcher { get; }
        public LifeMutationCoordinator Coordinator { get; }
        public PlanController Controller { get; }

        public Harness(string filePath)
        {
            Service = new LifePlanFileService(filePath);
            // Watcher is never Start()ed in tests; we drive OnFileReloaded directly.
            Watcher = new LifePlanWatcher(filePath);
            Coordinator = new LifeMutationCoordinator(Service);
            Controller = new PlanController(
                service: Service,
                watcher: Watcher,
                coordinator: Coordinator,
                channel: Channel,
                uiPost: action => action());
        }

        public PlanStateEnvelope SingleEnvelope() =>
            Channel.Posts.Should().ContainSingle().Which.Should().BeOfType<PlanStateEnvelope>().Subject;

        public void Dispose()
        {
            Controller.Dispose();
            Watcher.Dispose();
        }
    }

    private sealed class FakeBrowserChannel : IBrowserChannel
    {
        public List<object> Posts { get; } = new();

        public Task PostAsync(object envelope, CancellationToken ct = default)
        {
            Posts.Add(envelope);
            return Task.CompletedTask;
        }
    }
}
