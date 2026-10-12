namespace Xylocopadream.Audio;

/// <summary>
/// A sound decoded once, ready to mix: mono, 16 bits, at the rate of the mixer (4 times less memory than stereo floats:
/// a whole set of instruments can stay loaded).
/// </summary>
public sealed class Sound
{
    private Sound(short[] samples, int sampleRate)
    {
        Samples = samples;
        SampleRate = sampleRate;
    }

    public short[] Samples { get; }

    public int SampleRate { get; }

    public TimeSpan Duration => TimeSpan.FromSeconds(Samples.Length / (double)SampleRate);

    /// <summary>A WAV file, resampled to <paramref name="sampleRate"/> when it has another rate.</summary>
    public static Sound FromWav(Stream wav, int sampleRate)
    {
        (float[] samples, int rate) = WavDecoder.ReadMono(wav);
        return FromSamples(samples, rate, sampleRate);
    }

    /// <summary>Mono samples between -1 and 1, at <paramref name="rate"/>.</summary>
    public static Sound FromSamples(ReadOnlySpan<float> samples, int rate, int sampleRate)
    {
        float[] resampled = Resampler.Resample(samples, rate, sampleRate);
        short[] pcm = new short[resampled.Length];
        for (int i = 0; i < pcm.Length; i++)
            pcm[i] = (short)Math.Clamp(resampled[i] * short.MaxValue, short.MinValue, short.MaxValue);

        return new Sound(pcm, sampleRate);
    }
}
