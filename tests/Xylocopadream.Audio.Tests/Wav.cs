using System.Text;

namespace Xylocopadream.Audio.Tests;

/// <summary>WAV files written for the tests.</summary>
internal static class Wav
{
    /// <summary>A WAV file of one format, its frames given as raw bytes; an odd chunk before fmt checks the alignment.</summary>
    public static byte[] Create(ushort format, ushort channels, int rate, ushort bits, byte[] data, bool extensible = false)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(0); // patched below
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        writer.Write(Encoding.ASCII.GetBytes("junk"));
        writer.Write(3);
        writer.Write(new byte[] { 1, 2, 3, 0 }); // 3 bytes and the padding

        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(extensible ? 40 : 16);
        writer.Write(extensible ? (ushort)0xFFFE : format);
        writer.Write(channels);
        writer.Write(rate);
        writer.Write(rate * channels * bits / 8);
        writer.Write((ushort)(channels * bits / 8));
        writer.Write(bits);
        if (extensible)
        {
            writer.Write((ushort)22);
            writer.Write(bits);
            writer.Write(0); // channel mask
            writer.Write(format); // the sub format GUID starts with the real format
            writer.Write(new byte[14]);
        }

        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(data.Length);
        writer.Write(data);

        writer.Flush();
        byte[] file = stream.ToArray();
        BitConverter.GetBytes(file.Length - 8).CopyTo(file, 4);
        return file;
    }

    public static byte[] Pcm16(int rate, ushort channels, params short[] samples)
        => Create(1, channels, rate, 16, samples.SelectMany(BitConverter.GetBytes).ToArray());

    public static float[] Sine(double frequency, int rate, int length)
        => Enumerable.Range(0, length).Select(i => (float)Math.Sin(2 * Math.PI * frequency * i / rate)).ToArray();
}
