using System.Text.Json;
using Bishop.Life.Core.Speak;
using FluentAssertions;

namespace Bishop.Life.Tests;

public class SpeakPipeContractTests
{
    [Fact]
    public void StartedMessage_RoundTrips_WithAllFields()
    {
        var msg = new SpeakPipeMessage
        {
            Kind = SpeakPipeMessage.KindStarted,
            WavPath = @"C:\temp\foo.wav",
            PcmBase64 = Convert.ToBase64String(new byte[] { 1, 0, 2, 0, 3, 0 }),
            PcmSampleRateHz = 8000,
            DurationMs = 1234,
        };

        var json = JsonSerializer.Serialize(msg, SpeakPipeContract.JsonOptions);
        var back = JsonSerializer.Deserialize<SpeakPipeMessage>(json, SpeakPipeContract.JsonOptions);

        back.Should().NotBeNull();
        back!.Kind.Should().Be("started");
        back.WavPath.Should().Be(@"C:\temp\foo.wav");
        back.PcmBase64.Should().Be(msg.PcmBase64);
        back.PcmSampleRateHz.Should().Be(8000);
        back.DurationMs.Should().Be(1234);
    }

    [Fact]
    public void StartedMessage_UsesCamelCasePropertyNames()
    {
        var msg = new SpeakPipeMessage
        {
            Kind = SpeakPipeMessage.KindStarted,
            WavPath = "x",
            PcmBase64 = "AAAA",
            PcmSampleRateHz = 8000,
        };

        var json = JsonSerializer.Serialize(msg, SpeakPipeContract.JsonOptions);

        json.Should().Contain("\"kind\":");
        json.Should().Contain("\"wavPath\":");
        json.Should().Contain("\"pcmBase64\":");
        json.Should().Contain("\"pcmSampleRateHz\":");
    }

    [Fact]
    public void StoppedMessage_OmitsNullPayload()
    {
        var msg = new SpeakPipeMessage { Kind = SpeakPipeMessage.KindStopped };

        var json = JsonSerializer.Serialize(msg, SpeakPipeContract.JsonOptions);

        json.Should().NotContain("wavPath");
        json.Should().NotContain("pcmBase64");
    }

    [Fact]
    public void PipeName_IsStableConstant()
    {
        SpeakPipeContract.PipeName.Should().Be("bishop-life-speak");
    }
}
