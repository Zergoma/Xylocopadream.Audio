using System.Runtime.Versioning;

using NAudio.Wave;

namespace Xylocopadream.Audio.Wasapi;

/// <summary>
/// WASAPI in shared mode, event driven, with a short latency (NAudio): the output of Windows.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WasapiAudioOutput : IAudioOutput
{
    private const int LatencyMs = 40;

    private WasapiPlayer? _player;

    public void Start(IAudioSource source)
    {
        _player = new WasapiPlayerBuilder()
            .WithSharedMode()
            .WithEventSync()
            .WithLatency(LatencyMs)
            .Build();
        _player.Init(new SourceSampleProvider(source));
        _player.Play();
    }

    public void Dispose()
    {
        _player?.Stop();
        _player?.Dispose();
        _player = null;
    }

    /// <summary>
    /// The source as NAudio reads it.
    /// </summary>
    private sealed class SourceSampleProvider(IAudioSource source) : ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(source.SampleRate, source.Channels);

        public int Read(Span<float> buffer) => source.Read(buffer);
    }
}
