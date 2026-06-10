using Bishop.Life.Core.Speak;
using FluentAssertions;

namespace Bishop.Life.Tests;

public class WavPcmReaderTests
{
    [Fact]
    public void Read_OneSecondSineWave_DownsamplesTo8kHzMono()
    {
        var wav = BuildSineWav(sampleRate: 16000, durationSeconds: 1.0, amplitude: 0.5);

        var pcm = WavPcmReader.Read(new MemoryStream(wav));

        pcm.SampleRateHz.Should().Be(8000);
        pcm.DurationMs.Should().BeInRange(990, 1010);
        pcm.Samples.Length.Should().BeInRange(7900, 8100);
        // Sine should swing between positive and negative samples.
        pcm.Samples.Max().Should().BeGreaterThan(1000);
        pcm.Samples.Min().Should().BeLessThan(-1000);
    }

    [Fact]
    public void Read_StereoInput_IsMixedToMono()
    {
        var wav = BuildStereoSilenceWav(sampleRate: 16000, durationSeconds: 0.25);

        var pcm = WavPcmReader.Read(new MemoryStream(wav));

        pcm.Samples.Should().AllSatisfy(s => s.Should().Be(0));
    }

    [Fact]
    public void Read_SourceRateBelowTarget_KeepsSourceRate()
    {
        var wav = BuildSineWav(sampleRate: 4000, durationSeconds: 0.5, amplitude: 0.5);

        var pcm = WavPcmReader.Read(new MemoryStream(wav), targetSampleRateHz: 8000);

        pcm.SampleRateHz.Should().Be(4000);
        pcm.Samples.Length.Should().BeInRange(1900, 2100);
    }

    [Fact]
    public void Read_NonRiffStream_Throws()
    {
        var garbage = new byte[64];
        var act = () => WavPcmReader.Read(new MemoryStream(garbage));
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Read_NonPcm16Bit_Throws()
    {
        var wav = BuildSilenceWav(sampleRate: 22050, durationSeconds: 0.1, bitsPerSample: 8);
        var act = () => WavPcmReader.Read(new MemoryStream(wav));
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ToBase64_RoundTripsViaInt16LittleEndian()
    {
        var samples = new short[] { 1, -1, 256, -256, short.MaxValue, short.MinValue };

        var b64 = WavPcmReader.ToBase64(samples);
        var bytes = Convert.FromBase64String(b64);

        bytes.Length.Should().Be(samples.Length * 2);
        // Verify little-endian: sample 0 = 1 → 0x01 0x00
        bytes[0].Should().Be(0x01);
        bytes[1].Should().Be(0x00);
        // sample 2 = 256 = 0x0100 → 0x00 0x01
        bytes[4].Should().Be(0x00);
        bytes[5].Should().Be(0x01);
    }

    private static byte[] BuildSineWav(int sampleRate, double durationSeconds, double amplitude)
    {
        var totalFrames = (int)(sampleRate * durationSeconds);
        var samples = new short[totalFrames];
        const double freq = 440;
        for (int i = 0; i < totalFrames; i++)
        {
            var t = i / (double)sampleRate;
            samples[i] = (short)(amplitude * 32767 * Math.Sin(2 * Math.PI * freq * t));
        }
        return BuildPcmWav(samples, sampleRate, channels: 1, bitsPerSample: 16);
    }

    private static byte[] BuildStereoSilenceWav(int sampleRate, double durationSeconds)
    {
        var totalFrames = (int)(sampleRate * durationSeconds);
        var samples = new short[totalFrames * 2];
        return BuildPcmWav(samples, sampleRate, channels: 2, bitsPerSample: 16);
    }

    private static byte[] BuildSilenceWav(int sampleRate, double durationSeconds, int bitsPerSample = 16)
    {
        var totalFrames = (int)(sampleRate * durationSeconds);
        if (bitsPerSample == 16)
        {
            return BuildPcmWav(new short[totalFrames], sampleRate, channels: 1, bitsPerSample: 16);
        }
        var data = new byte[totalFrames];
        Array.Fill(data, (byte)128);
        return BuildPcmWavRaw(data, sampleRate, channels: 1, bitsPerSample: 8);
    }

    private static byte[] BuildPcmWav(short[] samples, int sampleRate, short channels, short bitsPerSample)
    {
        var data = new byte[samples.Length * 2];
        Buffer.BlockCopy(samples, 0, data, 0, data.Length);
        return BuildPcmWavRaw(data, sampleRate, channels, bitsPerSample);
    }

    private static byte[] BuildPcmWavRaw(byte[] data, int sampleRate, short channels, short bitsPerSample)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        var byteRate = sampleRate * channels * bitsPerSample / 8;
        short blockAlign = (short)(channels * bitsPerSample / 8);

        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        w.Write((uint)(36 + data.Length));
        w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);
        w.Write((short)1); // PCM
        w.Write(channels);
        w.Write(sampleRate);
        w.Write(byteRate);
        w.Write(blockAlign);
        w.Write(bitsPerSample);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        w.Write(data.Length);
        w.Write(data);
        return ms.ToArray();
    }
}
