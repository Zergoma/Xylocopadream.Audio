namespace Xylocopadream.Audio;

/// <summary>
/// What an <see cref="IAudioOutput"/> plays: 32-bit float samples, interleaved, read without end.
/// Independent of any audio library: each output adapts it to its own.
/// </summary>
public interface IAudioSource
{
    int SampleRate { get; }

    int Channels { get; }

    /// <summary>
    /// Fills <paramref name="buffer"/> (silence when nothing plays) and returns the number of samples written.
    /// Called on the audio thread.
    /// </summary>
    int Read(Span<float> buffer);
}
