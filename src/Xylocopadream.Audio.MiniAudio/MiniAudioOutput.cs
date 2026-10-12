using SoundFlow.Abstracts;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Devices;
using SoundFlow.Enums;
using SoundFlow.Structs;

namespace Xylocopadream.Audio.MiniAudio;

/// <summary>
/// miniaudio (through SoundFlow): the output of Windows (WASAPI), Linux (PulseAudio, ALSA...) and macOS (Core Audio).
/// Its mixer reads the source in short periods, for a short latency.
/// </summary>
public sealed class MiniAudioOutput : IAudioOutput
{
    private const uint PeriodMs = 10;

    private MiniAudioEngine? _engine;
    private AudioPlaybackDevice? _device;

    public void Start(IAudioSource source)
    {
        AudioFormat format = new()
        {
            Format = SampleFormat.F32,
            Channels = source.Channels,
            SampleRate = source.SampleRate,
        };

        _engine = new MiniAudioEngine();
        _device = _engine.InitializePlaybackDevice(null, format, new MiniAudioDeviceConfig { PeriodSizeInMilliseconds = PeriodMs });
        _device.MasterMixer.AddComponent(new SourceComponent(_engine, format, source));
        _device.Start();
    }

    public void Dispose()
    {
        _device?.Stop();
        _device?.Dispose();
        _device = null;
        _engine?.Dispose();
        _engine = null;
    }

    /// <summary>
    /// The source as a component of the miniaudio mixer.
    /// </summary>
    private sealed class SourceComponent(AudioEngine engine, AudioFormat format, IAudioSource source)
        : SoundComponent(engine, format)
    {
        public override string Name { get; set; } = "Xylocopadream.Audio";

        protected override void GenerateAudio(Span<float> buffer, int channels) => source.Read(buffer);
    }
}
