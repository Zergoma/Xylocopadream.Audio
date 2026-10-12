using System.Buffers.Binary;

namespace Xylocopadream.Audio;

/// <summary>
/// Reads a WAV file in mono: PCM in 8, 16, 24 or 32 bits, or 32-bit float (also in the extensible format);
/// the channels are averaged.
/// </summary>
public static class WavDecoder
{
    private const ushort Pcm = 1;
    private const ushort IeeeFloat = 3;
    private const ushort Extensible = 0xFFFE;

    /// <summary>The samples, between -1 and 1, and the sample rate of the file.</summary>
    public static (float[] Samples, int SampleRate) ReadMono(Stream stream)
    {
        using MemoryStream copy = new();
        stream.CopyTo(copy);
        return ReadMono(copy.GetBuffer().AsSpan(0, (int)copy.Length));
    }

    public static (float[] Samples, int SampleRate) ReadMono(ReadOnlySpan<byte> wav)
    {
        if (wav.Length < 12 || !wav[..4].SequenceEqual("RIFF"u8) || !wav[8..12].SequenceEqual("WAVE"u8))
            throw new InvalidDataException("Not a WAV file (RIFF/WAVE header missing).");

        ushort format = 0, channels = 0, bits = 0;
        int sampleRate = 0;
        ReadOnlySpan<byte> data = default;
        bool hasData = false;

        int at = 12;
        while (at + 8 <= wav.Length)
        {
            ReadOnlySpan<byte> id = wav.Slice(at, 4);
            int length = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(wav[(at + 4)..]), (uint)(wav.Length - at - 8));
            ReadOnlySpan<byte> chunk = wav.Slice(at + 8, length);

            if (id.SequenceEqual("fmt "u8) && chunk.Length >= 16)
            {
                format = BinaryPrimitives.ReadUInt16LittleEndian(chunk);
                channels = BinaryPrimitives.ReadUInt16LittleEndian(chunk[2..]);
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(chunk[4..]);
                bits = BinaryPrimitives.ReadUInt16LittleEndian(chunk[14..]);

                // the extensible format names the real one in the first two bytes of its sub format
                if (format == Extensible && chunk.Length >= 26)
                    format = BinaryPrimitives.ReadUInt16LittleEndian(chunk[24..]);
            }
            else if (id.SequenceEqual("data"u8))
            {
                data = chunk;
                hasData = true;
            }

            at += 8 + length + (length & 1); // chunks are aligned on two bytes
        }

        if (sampleRate <= 0 || channels == 0)
            throw new InvalidDataException("WAV file without a valid fmt chunk.");
        if (!hasData)
            throw new InvalidDataException("WAV file without a data chunk.");

        Func<ReadOnlySpan<byte>, float> read = (format, bits) switch
        {
            (Pcm, 8) => b => (b[0] - 128) / 128f,
            (Pcm, 16) => b => BinaryPrimitives.ReadInt16LittleEndian(b) / 32768f,
            (Pcm, 24) => b => ((b[0] | b[1] << 8 | (sbyte)b[2] << 16)) / 8388608f,
            (Pcm, 32) => b => BinaryPrimitives.ReadInt32LittleEndian(b) / 2147483648f,
            (IeeeFloat, 32) => BinaryPrimitives.ReadSingleLittleEndian,
            _ => throw new NotSupportedException($"WAV format {format} in {bits} bits is not supported (PCM 8/16/24/32 or float 32)."),
        };

        int sampleSize = bits / 8;
        int frameSize = sampleSize * channels;
        int frames = data.Length / frameSize;
        float[] samples = new float[frames];
        for (int frame = 0; frame < frames; frame++)
        {
            ReadOnlySpan<byte> bytes = data.Slice(frame * frameSize, frameSize);
            float sum = 0;
            for (int channel = 0; channel < channels; channel++)
                sum += read(bytes.Slice(channel * sampleSize, sampleSize));
            samples[frame] = sum / channels;
        }

        return (samples, sampleRate);
    }
}
