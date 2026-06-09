namespace Bishop.Life.Core.Speak;

/// <summary>
/// Reads a 16-bit PCM RIFF/WAV file and returns a downsampled RMS amplitude
/// envelope (one float in 0..1 per bucket). Sized for Piper TTS output —
/// supports mono or stereo, 16-bit PCM only. Skips unknown chunks until
/// <c>fmt </c> and <c>data</c> are found.
/// </summary>
public static class WavAmplitudeReader
{
    public sealed record Envelope(float[] Samples, int SampleRateHz, int DurationMs);

    public static Envelope Read(string wavPath, int amplitudesPerSecond)
    {
        using var fs = File.OpenRead(wavPath);
        return Read(fs, amplitudesPerSecond);
    }

    public static Envelope Read(Stream stream, int amplitudesPerSecond)
    {
        if (amplitudesPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(amplitudesPerSecond));

        using var br = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);

        if (new string(br.ReadChars(4)) != "RIFF")
            throw new InvalidDataException("Not a RIFF file.");
        br.ReadUInt32(); // riff size
        if (new string(br.ReadChars(4)) != "WAVE")
            throw new InvalidDataException("Not a WAVE file.");

        short channels = 0;
        int sampleRate = 0;
        short bitsPerSample = 0;
        long dataStart = -1;
        int dataLength = 0;

        while (stream.Position < stream.Length)
        {
            var idChars = br.ReadChars(4);
            if (idChars.Length < 4) break;
            var chunkId = new string(idChars);
            var chunkSize = br.ReadInt32();

            if (chunkId == "fmt ")
            {
                br.ReadInt16();         // audio format
                channels = br.ReadInt16();
                sampleRate = br.ReadInt32();
                br.ReadInt32();         // byte rate
                br.ReadInt16();         // block align
                bitsPerSample = br.ReadInt16();
                if (chunkSize > 16)
                    stream.Seek(chunkSize - 16, SeekOrigin.Current);
            }
            else if (chunkId == "data")
            {
                dataStart = stream.Position;
                dataLength = chunkSize;
                break;
            }
            else
            {
                stream.Seek(chunkSize, SeekOrigin.Current);
            }
        }

        if (dataStart < 0)
            throw new InvalidDataException("WAV has no data chunk.");
        if (bitsPerSample != 16)
            throw new NotSupportedException($"Only 16-bit PCM is supported (got {bitsPerSample}-bit).");
        if (channels < 1)
            throw new InvalidDataException("WAV reports no channels.");

        var bytesPerFrame = (bitsPerSample / 8) * channels;
        var totalFrames = dataLength / bytesPerFrame;
        var durationMs = (int)(1000L * totalFrames / sampleRate);

        var framesPerBucket = Math.Max(1, sampleRate / amplitudesPerSecond);
        var bucketCount = (totalFrames + framesPerBucket - 1) / framesPerBucket;
        var amplitudes = new float[bucketCount];

        stream.Position = dataStart;
        var buf = new byte[framesPerBucket * bytesPerFrame];

        for (int b = 0; b < bucketCount; b++)
        {
            var read = stream.Read(buf, 0, buf.Length);
            if (read <= 0) break;

            var frames = read / bytesPerFrame;
            double sumSq = 0;
            for (int f = 0; f < frames; f++)
            {
                int mix = 0;
                for (int c = 0; c < channels; c++)
                {
                    var idx = f * bytesPerFrame + c * 2;
                    short s = (short)(buf[idx] | (buf[idx + 1] << 8));
                    mix += s;
                }
                mix /= channels;
                var n = mix / 32768.0;
                sumSq += n * n;
            }
            amplitudes[b] = frames > 0 ? (float)Math.Sqrt(sumSq / frames) : 0f;
        }

        return new Envelope(amplitudes, sampleRate, durationMs);
    }
}
