using System.Collections.Concurrent;

namespace Xylocopadream.Audio;

/// <summary>
/// The mix read by an <see cref="IAudioOutput"/>: stereo, every sound playing added together, silence when none plays,
/// so that the output never stops and a sound starts at the next period. <see cref="Play"/> can be called from any
/// thread; <see cref="Read"/> is called by the audio thread only.
/// </summary>
public sealed class SoundMixer(int sampleRate) : IAudioSource
{
    private readonly ConcurrentQueue<Voice> _added = new();
    private readonly List<Voice> _playing = [];

    public int SampleRate { get; } = sampleRate;

    public int Channels => 2;

    /// <summary>Sounds playing now (read on the audio thread).</summary>
    internal int PlayingCount => _playing.Count;

    /// <summary>Plays a sound once, on both channels: mono samples at <see cref="SampleRate"/>.</summary>
    public void Play(Sound sound, float volume)
    {
        if (volume > 0 && sound.Samples.Length > 0)
            _added.Enqueue(new Voice(sound.Samples, volume));
    }

    public int Read(Span<float> buffer)
    {
        while (_added.TryDequeue(out Voice? voice))
            _playing.Add(voice);

        buffer.Clear();
        int frames = buffer.Length / Channels;
        for (int v = _playing.Count - 1; v >= 0; v--)
        {
            if (!_playing[v].MixInto(buffer, frames))
                _playing.RemoveAt(v);
        }

        // the whole buffer, always: the output keeps going
        return frames * Channels;
    }

    private sealed class Voice(short[] samples, float volume)
    {
        private readonly float _gain = volume / short.MaxValue;
        private int _position;

        /// <summary>Adds the next frames; false once the sound is over.</summary>
        public bool MixInto(Span<float> buffer, int frames)
        {
            int count = Math.Min(frames, samples.Length - _position);
            for (int i = 0; i < count; i++)
            {
                float value = samples[_position + i] * _gain;
                buffer[2 * i] += value;
                buffer[2 * i + 1] += value;
            }

            _position += count;
            return _position < samples.Length;
        }
    }
}
