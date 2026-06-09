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
            Samples = new[] { 0.1f, 0.2f, 0.3f },
            SampleRateHz = 40,
            DurationMs = 1234,
        };

        var json = JsonSerializer.Serialize(msg, SpeakPipeContract.JsonOptions);
        var back = JsonSerializer.Deserialize<SpeakPipeMessage>(json, SpeakPipeContract.JsonOptions);

        back.Should().NotBeNull();
        back!.Kind.Should().Be("started");
        back.WavPath.Should().Be(@"C:\temp\foo.wav");
        back.Samples.Should().Equal(0.1f, 0.2f, 0.3f);
        back.SampleRateHz.Should().Be(40);
        back.DurationMs.Should().Be(1234);
    }

    [Fact]
    public void StartedMessage_UsesCamelCasePropertyNames()
    {
        var msg = new SpeakPipeMessage
        {
            Kind = SpeakPipeMessage.KindStarted,
            WavPath = "x",
            SampleRateHz = 40,
        };

        var json = JsonSerializer.Serialize(msg, SpeakPipeContract.JsonOptions);

        json.Should().Contain("\"kind\":");
        json.Should().Contain("\"wavPath\":");
        json.Should().Contain("\"sampleRateHz\":");
    }

    [Fact]
    public void StoppedMessage_OmitsNullPayload()
    {
        var msg = new SpeakPipeMessage { Kind = SpeakPipeMessage.KindStopped };

        var json = JsonSerializer.Serialize(msg, SpeakPipeContract.JsonOptions);

        json.Should().NotContain("wavPath");
        json.Should().NotContain("samples");
    }

    [Fact]
    public void PipeName_IsStableConstant()
    {
        SpeakPipeContract.PipeName.Should().Be("bishop-life-speak");
    }
}
