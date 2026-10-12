namespace Xylocopadream.Audio;

/// <summary>
/// The sound card, reading a source without end (silence when nothing plays):
/// MiniAudioOutput (every platform) or WasapiAudioOutput (Windows), in their own packages.
/// </summary>
public interface IAudioOutput : IDisposable
{
    /// <summary>
    /// Opens the device and plays <paramref name="source"/> until disposed. Throws when no device can be opened.
    /// </summary>
    void Start(IAudioSource source);
}
