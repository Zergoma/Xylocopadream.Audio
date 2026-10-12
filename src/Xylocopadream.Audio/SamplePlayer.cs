using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Xylocopadream.Audio;

/// <summary>
/// Plays short sounds by name (WAV files), at once and overlapping, through one output that never stops: the device
/// stays awake, so a sound is never clipped at its start. The sounds are decoded once and kept.
/// Opening the device takes about half a second: call <see cref="PreloadAsync"/> at startup, off the UI thread.
/// One per app (a singleton); disposing it closes the output.
/// </summary>
public sealed class SamplePlayer : IDisposable
{
    /// <summary>The rate of the mix: the one of most sound files (the output converts it for the device).</summary>
    public const int DefaultSampleRate = 44100;

    private readonly IAudioOutput _output;
    private readonly Func<string, Task<Stream>> _open;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, Lazy<Task<Sound>>> _sounds = new();
    private readonly Lazy<SoundMixer?> _mixer;
    private readonly int _sampleRate;

    /// <param name="output">The sound card: MiniAudioOutput, WasapiAudioOutput...</param>
    /// <param name="open">Opens a sound by its name (a file, an asset of the app, a resource).</param>
    public SamplePlayer(IAudioOutput output, Func<string, Task<Stream>> open, ILogger<SamplePlayer>? logger = null,
        int sampleRate = DefaultSampleRate)
    {
        _output = output;
        _open = open;
        _logger = logger ?? NullLogger<SamplePlayer>.Instance;
        _sampleRate = sampleRate;
        _mixer = new Lazy<SoundMixer?>(StartOutput);
    }

    /// <summary>False when no device could be opened: <see cref="Play"/> does nothing.</summary>
    public bool IsAvailable => _mixer.Value is not null;

    /// <summary>
    /// Opens the device and loads the sounds, so that <see cref="Play"/> starts them at once. Sounds already loaded
    /// are skipped. A sound that cannot be loaded is logged and left out.
    /// </summary>
    public async Task PreloadAsync(params string[] sounds)
    {
        await Task.Run(() => _mixer.Value);
        await Task.WhenAll(sounds.Select(async sound =>
        {
            try
            {
                await GetSoundAsync(sound);
            }
            catch (Exception)
            {
                // logged by LoadAsync; the others are still loaded
            }
        }));
    }

    /// <summary>
    /// Plays a sound at <paramref name="volume"/> (0 to 1). A sound not loaded yet starts loading and is skipped this
    /// time: a late sound would be worse than none.
    /// </summary>
    public void Play(string sound, double volume)
    {
        if (volume <= 0 || _mixer.Value is not SoundMixer mixer)
            return;

        Task<Sound> loading = GetSoundAsync(sound);
        if (loading.IsCompletedSuccessfully)
            mixer.Play(loading.Result, (float)volume);
    }

    public void Dispose() => _output.Dispose();

    private SoundMixer? StartOutput()
    {
        try
        {
            SoundMixer mixer = new(_sampleRate);
            _output.Start(mixer);
            _logger.LogInformation("Audio output {Output} started", _output.GetType().Name);
            return mixer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to start the audio output, sounds are disabled");
            _output.Dispose();
            return null;
        }
    }

    private Task<Sound> GetSoundAsync(string sound)
    {
        Lazy<Task<Sound>> entry = _sounds.GetOrAdd(sound, NewEntry);

        // a sound that failed is loaded again now (unless another thread already did)
        if (entry.Value.IsFaulted)
        {
            Lazy<Task<Sound>> retry = NewEntry(sound);
            entry = _sounds.TryUpdate(sound, retry, entry) ? retry : _sounds.GetOrAdd(sound, NewEntry);
        }

        return entry.Value;
    }

    private Lazy<Task<Sound>> NewEntry(string sound) => new(() => LoadAsync(sound));

    private async Task<Sound> LoadAsync(string sound)
    {
        try
        {
            using MemoryStream buffer = new();
            await using (Stream file = await _open(sound))
            {
                await file.CopyToAsync(buffer);
            }

            buffer.Position = 0;
            // decoding and resampling take a while: not on the UI thread
            return await Task.Run(() => Sound.FromWav(buffer, _sampleRate));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to load sound {Sound}", sound);
            throw;
        }
    }
}
