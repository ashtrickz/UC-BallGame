# Game Audio

The selected sounds are connected to the existing AudioService. The main set is the approved v02. The jump and shot sounds come from the selected `jump.mp3` and `shot.mp3` files. Their SHA-256 hashes matched saved takes, so the matching standalone WAV masters were used instead of re-compressing the MP3s. The three jump variants and the two shot variants are not merged into one long sound.

## Library

`Assets/_Game/Resources/Audio/AudioLibrary.asset` has 32 entries: 30 sound events with 68 clips, and two music themes. `MainMenuMusic` and `GameplayMusic` currently use the selected track `Music/Curious Pattern.mp3`; each theme's clip can be replaced separately if needed. The clips live in `Assets/_Game/Audio/Selected`.

Each entry has:

- `Id`: the game event, with one entry per ID. Assign music to the existing MainMenuMusic and GameplayMusic rows. An empty duplicate does not block a filled entry, but duplicates should not be created.
- `Clip` and `Variants`: the main clip and the extra variants. One call plays one clip, and the same variant never repeats back to back.
- `Volume`: the base volume.
- `Pitch Range`: a random pitch at start. Consecutive values differ, and the audio randomizer does not consume the level RNG. Equal bounds turn the variation off. Loops and music default to 1…1; one-shot effects have a small spread.
- `Group`: the logical channel, UI, Sfx or Music (the existing Button, Ambience and Voice are also supported).
- `Mixer Group`: an optional per-entry AudioMixerGroup reference. An empty field uses the library's shared route.
- `Loop`, `Min Interval`, `Max Voices`: looping, the minimum interval and the overlap limit.

Source volume = base Volume × current playback multiplier × Master × the volume of the matching settings channel. The mixer parameters do not duplicate these multipliers.

## When the mixer is ready

At the top of the AudioLibrary there is `Mixer Routes` with Sfx, UI and Music rows. Drag the AudioMixerGroups into their `Output` fields. A single sound can be sent to another group through its own `Mixer Group` if needed. References are assigned in the editor, and code applies the route at playback.

The new routes require no specific group names or exposed parameters. While the references are empty, sound plays directly through the AudioListener and the settings sliders keep working.

ProjectScope uses the AudioLibrary assigned to it; if the field is empty, it loads this library from the Resources path `Audio/AudioLibrary`. Game scenes therefore need no separate copies of the setup.

## Game events

SlimeView reports the actual wind-up, take-off and landing, droplet release, hit, pop and the dive into the pool. CrystalFieldView reports the moment of breaking, and DoorsView reports the doors opening. GameplayAudioPresenter maps them to the library; the model and the generator do not depend on audio.

`PoolSplash` plays once when the slime touches the water during the victory dive, together with the visual splash. It uses the existing wet `Shot/slime_shoot_02.wav` with its own settings: Volume 0.9, Pitch Range 0.8…0.9, Sfx. It does not depend on the volume, cooldown or voice limit of projectile hits.

MainMenuMusic starts when audio initializes and when the player returns from the game. GameplayMusic starts at the beginning of a game attempt. Both themes share one dedicated source: switching replaces the track, and calling the theme that is already playing does not restart it. If a clip was missing, ChangeTheme can be called again with the same value after the clip is assigned.

The charge and flight loops belong to the current attempt. Cancelling a charge stops them. Pause freezes gameplay SFX and keeps their position, and resuming does not lift an owner's manual pause. UI and music stay available during pause. A restart or a scene unload stops the sources of the old attempt. Victory and defeat sounds play together with the result. The low-resource warning plays when the slime enters the risk zone, not continuously.

UiClickSounds handles common button presses, toggles and slider steps. The special UiConfirm and UiBack entries and the rest are available for explicit calls. There are no coin sounds, because the selected game set has no event for them.

## Calling from code

```csharp
var handle = audio.PlaySound(GameSound.SlimeJump);
handle?.SetVolume(0.7f); // relative to the entry's base volume
handle?.SetPitch(1.02f);
handle?.Pause();
handle?.Resume();
handle?.Stop();
```

Changes through an old handle never affect a new sound when its AudioSource has already been reused. AudioService keeps the earlier PlayClip and PlayUiSound methods and the Addressable paths for older libraries.

## Reproducible setup

`Tools → Ball Game → Audio → Install Selected Library` imports the selected WAV files and creates any missing entries. Running it again keeps the volumes, pitch, clips and mixer references that are already set. It does not create a mixer or rebuild scenes.

The project keeps only the 69 audio files in use: 68 short WAV effect variants and one music MP3. Several events may share the same clips. Older generations, editing sources, previews and generation reports were removed, and those folders are in `.gitignore`. The GUIDs and importer settings of the remaining clips, the library and the mixer were kept.

The hashes of the selected clips and the licence of the original wet sample are in `Art/Audio/Selected/import-manifest.json`. Its `source` fields are historical paths of removed takes, and `path` points to the kept game clip. The initial assignment spec for the installer is `Art/Audio/Selected/library.json`.

Automated checks are in AudioServiceTests: library assignment and ID uniqueness, single clips instead of edits, pitch and clip variation, base volume and channels, pause, voice limits and protection of reused sources. The music checks cover empty duplicates, retrying after a missing clip is assigned, and switching themes without restarting a theme that is already playing.
