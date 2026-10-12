namespace Xylocopadream.Audio.Tests;

public sealed class PlaybackTests
{
    private static readonly Sound s_half = Sound.FromSamples([0.5f, 0.5f, 0.5f], 8000, 8000);
    private static readonly Sound s_quarter = Sound.FromSamples([0.25f], 8000, 8000);

    [Fact]
    public void Mixer_outputs_silence_when_nothing_plays()
    {
        SoundMixer mixer = new(8000);
        float[] buffer = Enumerable.Repeat(9f, 8).ToArray();

        Assert.Equal(8, mixer.Read(buffer));
        Assert.All(buffer, sample => Assert.Equal(0f, sample));
    }

    [Fact]
    public void Sounds_overlap_on_both_channels_then_end()
    {
        SoundMixer mixer = new(8000);
        mixer.Play(s_half, 1f);
        mixer.Play(s_quarter, 0.5f);
        mixer.Play(s_half, 0f); // silent: not even added

        float[] buffer = new float[4];
        mixer.Read(buffer);
        Assert.Equal(0.5f + 0.125f, buffer[0], 3);
        Assert.Equal(buffer[0], buffer[1]);
        Assert.Equal(0.5f, buffer[2], 3);
        Assert.Equal(1, mixer.PlayingCount); // the short one is over

        mixer.Read(buffer);
        Assert.Equal(0.5f, buffer[0], 3);
        Assert.Equal(0f, buffer[2]);
        Assert.Equal(0, mixer.PlayingCount);
    }

    [Fact]
    public async Task Player_opens_the_output_once_and_plays_loaded_sounds()
    {
        FakeOutput output = new();
        using SamplePlayer player = new(output, name => Task.FromResult<Stream>(new MemoryStream(Wav.Pcm16(44100, 1, 16384, 16384))));

        await player.PreloadAsync("a.wav", "b.wav");
        player.Play("a.wav", 1);
        player.Play("a.wav", 1);

        Assert.True(player.IsAvailable);
        Assert.Equal(1, output.Starts);
        float[] buffer = new float[2];
        output.Source!.Read(buffer);
        Assert.Equal(1f, buffer[0], 3); // two at 0.5
    }

    [Fact]
    public async Task A_sound_not_loaded_yet_is_skipped_and_loaded()
    {
        FakeOutput output = new();
        TaskCompletionSource<Stream> file = new();
        using SamplePlayer player = new(output, _ => file.Task);
        await player.PreloadAsync();

        player.Play("late.wav", 1);
        file.SetResult(new MemoryStream(Wav.Pcm16(44100, 1, 16384)));
        await player.PreloadAsync("late.wav");
        player.Play("late.wav", 1);

        float[] buffer = new float[2];
        output.Source!.Read(buffer);
        Assert.Equal(0.5f, buffer[0], 3); // once: the first call was skipped
    }

    [Fact]
    public async Task Without_a_device_nothing_plays_and_nothing_throws()
    {
        FakeOutput output = new() { Fails = true };
        using SamplePlayer player = new(output, _ => Task.FromResult<Stream>(new MemoryStream(Wav.Pcm16(44100, 1, 1))));

        await player.PreloadAsync("a.wav");
        player.Play("a.wav", 1);

        Assert.False(player.IsAvailable);
        Assert.True(output.Disposed);
    }

    [Fact]
    public async Task A_broken_sound_is_left_out_and_tried_again()
    {
        int opened = 0;
        using SamplePlayer player = new(new FakeOutput(), _ =>
        {
            opened++;
            return Task.FromResult<Stream>(new MemoryStream("broken"u8.ToArray()));
        });

        await player.PreloadAsync("broken.wav");
        await player.PreloadAsync("broken.wav");

        Assert.Equal(2, opened);
    }

    private sealed class FakeOutput : IAudioOutput
    {
        public bool Fails { get; init; }

        public int Starts { get; private set; }

        public bool Disposed { get; private set; }

        public IAudioSource? Source { get; private set; }

        public void Start(IAudioSource source)
        {
            if (Fails)
                throw new InvalidOperationException("no device");

            Starts++;
            Source = source;
        }

        public void Dispose() => Disposed = true;
    }
}
