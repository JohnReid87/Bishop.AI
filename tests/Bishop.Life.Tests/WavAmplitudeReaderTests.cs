using Bishop.Life.Core.Speak;
using FluentAssertions;

namespace Bishop.Life.Tests;

public class WavAmplitudeReaderTests
{
    [Fact]
    public void Read_OneSecondSineWave_ReturnsExpectedRateAndDuration()
    {
        var wav = BuildSineWav(sampleRate: 16000, durationSeconds: 1.0, amplitude: 0.5);

        var env = WavAmplitudeReader.Read(new MemoryStream(wav), amplitudesPerSecond: 40);

        env.SampleRateHz.Should().Be(16000);
        env.DurationMs.Should().BeInRange(990, 1010);
        env.Samples.Length.Should().BeInRange(38, 42);
        // RMS of a 0.5-amplitude sine is 0.5/sqrt(2) ≈ 0.354.
        env.Samples.Average().Should().BeApproximately(0.354f, 0.05f);
    }

    [Fact]
    public void Read_SilentWav_ReturnsZeroAmplitudes()
    {
        var wav = BuildSilenceWav(sampleRate: 22050, durationSeconds: 0.5);

        var env = WavAmplitudeReader.Read(new MemoryStream(wav), amplitudesPerSecond: 40);

        env.Samples.Should().AllSatisfy(a => a.Should().Be(0f));
    }

    [Fact]
    public void Read_NonRiffStream_Throws()
    {
        var garbage = new byte[64];
        var act = () => WavAmplitudeReader.Read(new MemoryStream(garbage), 40);
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Read_NonPcm16Bit_Throws()
    {
        var wav = BuildSilenceWav(sampleRate: 22050, durationSeconds: 0.1, bitsPerSample: 8);
        var act = () => WavAmplitudeReader.Read(new MemoryStream(wav), 40);
        act.Should().Throw<NotSupportedException>();
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

    private static byte[] BuildSilenceWav(int sampleRate, double durationSeconds, int bitsPerSample = 16)
    {
        var totalFrames = (int)(sampleRate * durationSeconds);
        if (bitsPerSample == 16)
        {
            return BuildPcmWav(new short[totalFrames], sampleRate, channels: 1, bitsPerSample: 16);
        }
        // 8-bit unsigned PCM, midpoint = 128 = silence.
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
