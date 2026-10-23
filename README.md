# Xylocopadream.Audio

Short sounds played at once and overlapping: key clicks, notes, game effects, notifications.

One output that never stops (silence when nothing plays) keeps the device awake: a sound starts at the next period,
never clipped at its start. The sounds are decoded once (WAV), resampled to the rate of the mix and kept in mono
16 bits.

| Package | Output | Platforms |
| --- | --- | --- |
| `Xylocopadream.Audio` | the player, the mixer, WAV decoding, resampling | all (no dependency but logging abstractions) |
| `Xylocopadream.Audio.MiniAudio` | `MiniAudioOutput`: miniaudio through SoundFlow, 10 ms periods | Windows, Linux, macOS |
| `Xylocopadream.Audio.Wasapi` | `WasapiAudioOutput`: WASAPI shared mode, event driven, 40 ms, through NAudio | Windows (WPF, WinUI, MAUI, Avalonia) |

## Use

The packages are published to the private GitHub Packages feed of the Xylocopadream organization
(`https://nuget.pkg.github.com/Xylocopadream/index.json`), never to nuget.org.

```
dotnet add package Xylocopadream.Audio.MiniAudio     # or Xylocopadream.Audio.Wasapi
```

```csharp
// one for the app (a singleton): disposing it closes the device
var player = new SamplePlayer(new MiniAudioOutput(), name => Task.FromResult<Stream>(File.OpenRead(name)), logger);

// at startup, off the UI thread: opens the device (about half a second) and decodes the sounds
await player.PreloadAsync("sounds/ok.wav", "sounds/error.wav");

// at once, from any thread; sounds overlap
player.Play("sounds/ok.wav", volume: 0.8);
```

With dependency injection:

```csharp
services.AddSingleton<IAudioOutput, MiniAudioOutput>();
services.AddSingleton(provider => new SamplePlayer(
    provider.GetRequiredService<IAudioOutput>(), OpenSound, provider.GetService<ILogger<SamplePlayer>>()));
```

- `Play` on a sound not loaded yet starts loading it and skips it this time (a late sound is worse than none).
- Without a device (none plugged, none allowed), `IsAvailable` is false and `Play` does nothing; the error is logged.
- A sound that cannot be loaded is logged, left out, and loaded again when asked again.

### Formats

WAV: PCM 8, 16, 24 or 32 bits, or 32-bit float, also in the extensible format; any number of channels (averaged to
mono), any rate (resampled with a windowed sinc to `SamplePlayer.DefaultSampleRate`, 44100 Hz, unless another rate is
given).

### Your own output

Implement `IAudioOutput`: `Start(IAudioSource)` opens the device and reads the source (32-bit float, interleaved
stereo) on the audio thread until disposed.

## Publish a new version

```
.\publish.ps1 -Bump                                           # local feed (%UserProfile%\nuget_local)
.\publish.ps1 -GitHub                                         # tests, then GitHub Packages (token: GITHUB_PACKAGES_TOKEN)
```

## License

MIT. Uses [SoundFlow](https://github.com/LSXPrime/SoundFlow) (MIT, miniaudio) and [NAudio](https://github.com/naudio/NAudio) (MIT).
