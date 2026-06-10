using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bishop.Life.App.Speak;
using Bishop.Life.Core.Schema.Envelopes;
using Bishop.Life.Core.Speak;
using Bishop.Life.Core.Web;
using FluentAssertions;

namespace Bishop.Life.Tests;

public class SpeakControllerTests
{
    private sealed class FakeBrowserChannel : IBrowserChannel
    {
        public List<object> Posts { get; } = new();

        public Task PostAsync(object envelope, CancellationToken ct = default)
        {
            Posts.Add(envelope);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task HandleAsync_StartedMessage_PostsSpeakStartedEnvelope()
    {
        var channel = new FakeBrowserChannel();
        using var controller = new SpeakController(new LifeSpeakPipeServer(), new LifeSpeakPlayer(), channel);
        var msg = new SpeakPipeMessage
        {
            Kind = SpeakPipeMessage.KindStarted,
            PcmBase64 = "AAAA",
            PcmSampleRateHz = 16000,
            DurationMs = 250,
        };

        await controller.HandleAsync(msg);

        channel.Posts.Should().HaveCount(1);
        var envelope = channel.Posts[0].Should().BeOfType<SpeakEnvelope>().Subject;
        envelope.Type.Should().Be("speak.started");
        envelope.PcmBase64.Should().Be("AAAA");
        envelope.PcmSampleRateHz.Should().Be(16000);
        envelope.DurationMs.Should().Be(250);
    }

    [Fact]
    public async Task HandleAsync_StoppedMessage_PostsSpeakStoppedEnvelope()
    {
        var channel = new FakeBrowserChannel();
        using var controller = new SpeakController(new LifeSpeakPipeServer(), new LifeSpeakPlayer(), channel);
        var msg = new SpeakPipeMessage { Kind = SpeakPipeMessage.KindStopped };

        await controller.HandleAsync(msg);

        channel.Posts.Should().HaveCount(1);
        var envelope = channel.Posts[0].Should().BeOfType<SpeakEnvelope>().Subject;
        envelope.Type.Should().Be("speak.stopped");
        envelope.PcmBase64.Should().BeNull();
    }
}
