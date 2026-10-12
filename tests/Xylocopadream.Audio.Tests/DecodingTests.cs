namespace Xylocopadream.Audio.Tests;

public sealed class DecodingTests
{
    [Fact]
    public void Pcm16_stereo_is_averaged_to_mono()
    {
        byte[] wav = Wav.Pcm16(44100, 2, 16384, 0, -32768, -32768);

        (float[] samples, int rate) = WavDecoder.ReadMono(wav);

        Assert.Equal(44100, rate);
        Assert.Equal([0.25f, -1f], samples);
    }

    [Fact]
    public void Pcm8_24_32_and_float_are_read()
    {
        Assert.Equal([0f, -1f], WavDecoder.ReadMono(Wav.Create(1, 1, 8000, 8, [128, 0])).Samples);
        Assert.Equal([0.5f, -0.5f], WavDecoder.ReadMono(Wav.Create(1, 1, 8000, 24, [0, 0, 0x40, 0, 0, 0xC0])).Samples);
        Assert.Equal([0.5f], WavDecoder.ReadMono(Wav.Create(1, 1, 8000, 32, BitConverter.GetBytes(0x40000000))).Samples);
        Assert.Equal([0.75f], WavDecoder.ReadMono(Wav.Create(3, 1, 8000, 32, BitConverter.GetBytes(0.75f))).Samples);
        Assert.Equal([0.75f], WavDecoder.ReadMono(Wav.Create(3, 1, 8000, 32, BitConverter.GetBytes(0.75f), extensible: true)).Samples);
    }

    [Fact]
    public void Other_files_are_refused()
    {
        Assert.Throws<InvalidDataException>(() => WavDecoder.ReadMono("not a wav file"u8));
        Assert.Throws<NotSupportedException>(() => WavDecoder.ReadMono(Wav.Create(2, 1, 8000, 4, [0]))); // ADPCM
    }

    [Fact]
    public void Same_rate_keeps_the_samples()
    {
        float[] input = [0.1f, -0.2f, 0.3f];
        Assert.Equal(input, Resampler.Resample(input, 44100, 44100));
    }

    [Theory]
    [InlineData(48000, 44100)]
    [InlineData(22050, 44100)]
    public void Resampling_keeps_the_pitch_and_the_length(int from, int to)
    {
        float[] input = Wav.Sine(1000, from, from / 10); // 100 ms of 1 kHz

        float[] output = Resampler.Resample(input, from, to);

        Assert.Equal(to / 10, output.Length);
        float[] expected = Wav.Sine(1000, to, output.Length);
        // away from the edges, where the filter lacks samples on one side
        for (int i = 100; i < output.Length - 100; i++)
            Assert.True(Math.Abs(expected[i] - output[i]) < 0.01, $"sample {i}: {output[i]} instead of {expected[i]}");
    }

    [Fact]
    public void Going_down_removes_what_the_new_rate_cannot_hold()
    {
        // 20 kHz cannot exist at 22050 Hz (above 11025): it must not come back as a lower tone
        float[] output = Resampler.Resample(Wav.Sine(20000, 48000, 4800), 48000, 22050);

        float loudest = output.Skip(100).SkipLast(100).Max(Math.Abs);
        Assert.True(loudest < 0.05, $"{loudest}");
    }

    [Fact]
    public void A_sound_is_kept_in_16_bits_at_the_mixer_rate()
    {
        Sound sound = Sound.FromWav(new MemoryStream(Wav.Pcm16(48000, 1, new short[4800])), 44100);

        Assert.Equal(44100, sound.SampleRate);
        Assert.Equal(4410, sound.Samples.Length);
        Assert.Equal(TimeSpan.FromMilliseconds(100), sound.Duration);
    }
}
