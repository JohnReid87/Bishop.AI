namespace Bishop.Life.Core.Speak;

/// <summary>
/// Reads a 16-bit PCM RIFF/WAV file and returns a mono <see cref="short"/>
/// sample buffer downsampled to a target rate (defaults to 8 kHz — plenty of
/// detail for a speech oscilloscope viz while keeping the pipe payload small,
/// roughly 16 KB per second of speech once base64-encoded). Supports mono or
/// stereo input, 16-bit PCM only. Skips unknown chunks until <c>fmt </c> and
/// <c>data</c> are found.
/// </summary>
public static class WavPcmReader
{
    public const int DefaultTargetRateHz = 8000;

    public sealed record Pcm(short[] Samples, int SampleRateHz, int DurationMs);

    public static Pcm Read(string wavPath, int targetSampleRateHz = DefaultTargetRateHz)
    {
        using var fs = File.OpenRead(wavPath);
        return Read(fs, targetSampleRateHz);
    }

    public static Pcm Read(Stream stream, int targetSampleRateHz = DefaultTargetRateHz)
    {
        if (targetSampleRateHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetSampleRateHz));

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

        // Decimate by bucket-averaging: pick the target rate's frame count and
        // average each input bucket down to one mono sample. Aliasing is fine
        // here — the visualisation only needs amplitude+shape character, not
        // audio-grade fidelity.
        var outRate = Math.Min(targetSampleRateHz, sampleRate);
        var outFrameCount = (int)(1L * totalFrames * outRate / sampleRate);
        if (outFrameCount < 1) outFrameCount = totalFrames > 0 ? 1 : 0;
        var output = new short[outFrameCount];

        stream.Position = dataStart;
        // Read the whole data chunk in one shot — Piper utterances are short
        // (a few seconds) and this code runs once per utterance, so the
        // simplicity wins over streaming.
        var data = br.ReadBytes(dataLength);

        for (int o = 0; o < outFrameCount; o++)
        {
            var fStart = (int)(1L * o * totalFrames / outFrameCount);
            var fEnd = (int)(1L * (o + 1) * totalFrames / outFrameCount);
            if (fEnd <= fStart) fEnd = fStart + 1;
            if (fEnd > totalFrames) fEnd = totalFrames;

            long sum = 0;
            int n = 0;
            for (int f = fStart; f < fEnd; f++)
            {
                int mix = 0;
                for (int c = 0; c < channels; c++)
                {
                    var idx = f * bytesPerFrame + c * 2;
                    short s = (short)(data[idx] | (data[idx + 1] << 8));
                    mix += s;
                }
                mix /= channels;
                sum += mix;
                n++;
            }
            output[o] = n > 0 ? (short)(sum / n) : (short)0;
        }

        return new Pcm(output, outRate, durationMs);
    }

    /// <summary>
    /// Encode the sample array as base64 of little-endian Int16 bytes, ready
    /// to drop into <c>SpeakPipeMessage.PcmBase64</c>.
    /// </summary>
    public static string ToBase64(short[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return Convert.ToBase64String(bytes);
    }
}
