# Demo Application

The repository includes an Avalonia desktop demo application, `DemaConsulting.Speech.Demo`, which
uses only the library's public API. It is not published to NuGet; run it from a clone of the
repository:

```bash
dotnet run --project src/DemaConsulting.Speech.Demo
```

The application opens with four panels, one of which embeds a fifth:

- **Audio Devices**: lists the capture and playback devices reported by `AudioDeviceFactory`'s
  probes, preselects the first device in each direction, and offers a refresh button that
  forces a hardware re-scan via `AudioDeviceFactory.RefreshDevices()`, so a headset
  connected after start-up reliably appears without restarting the application. If a capture or
  playback device is currently started when refresh is clicked, the refresh is refused with an
  explanatory status message rather than crashing. Each picker reports how many devices were
  found; when a direction reports none, it explains that the audio backend may be unavailable or
  no device of that kind is connected, rather than showing a blank list.
- **Model Catalog**: lists the models `SpeechModelCatalog` knows about, each with its
  installation state (not downloaded, downloading, installed, or failed or corrupt), and offers a
  download action that reports progress and reports failure with an explanation. A
  **Reference voices** section lists the optional reference voice pack for the Chatterbox Turbo
  voice-cloning model (`voice-zero-1.0`, about 570 MiB download, about 572 MiB installed with emotion variants or about
  44 MiB without - an **Include emotion variants** checkbox chooses, **Add emotions** upgrades a
  voices-only install - CC0 1.0 Universal, attribution
  "voice-zero by OwenTyme") with Install and Remove buttons, progress and failure reporting, the
  consent note, and the folder for your own `.wav`/`.flac` voices. Installing or removing a pack
  rebuilds the catalog, so it is refused while a model is downloading (and model downloads and
  mirror changes are refused while a pack installs).
- **Text-to-Speech**: lists installed synthesis models, offers a text box with inline hints
  showing a few of the library's Natural Language Audio Tags (such as `[whispers]`,
  `[short pause]`, and `[excited]`), and Play/Stop controls that compose an
  `ISpeechSynthesizerEngine`/`ISynthesisSession` through a demo-owned seam over
  `SpeechSynthesizerFactory`. The panel caches its engine and session across Play calls -
  reloading the engine only when the selected model or its parameter values change, and
  recreating the session only when the engine was just reloaded or the playback device changes -
  rather than recreating a synthesizer on every Play. Playback status is derived from the
  session's own `State`/`StateChanged` (synthesizing, playing, idle, or an honest error) rather
  than tracked ad hoc; the panel explains itself if no synthesis model is installed or no
  playback device is available. The panel refreshes itself
  automatically the moment a synthesis model finishes downloading from the Model Catalog panel,
  with no manual click or application restart needed. The model picker and its Model Settings
  controls disable while audio is synthesizing or playing, so a voice/speaker cannot be changed
  mid-playback. When the Chatterbox Turbo model is selected the voice dropdown lists the
  installed pack voices and your own voices, refreshes immediately after a pack is installed or
  removed (keeping your other settings), and a note below it states the consent reminder (only
  clone voices you have permission to use) and the attribution of each installed pack.
- **Speech-to-Text**: offers Start/Stop streaming transcription that composes an
  `ISpeechRecognizerEngine`/`IRecognitionSession` through a demo-owned seam over
  `SpeechRecognizerFactory`. Start loads/reuses the cached engine, creates a fresh session (a
  recognition session is single-use), and pumps `GetResultsAsync` on a background task; Stop
  requests `StopAsync` on the active session. Listening state is derived from the session's own
  `State`/`StateChanged` rather than tracked ad hoc. Committed final
  results accumulate in order while a trailing partial line updates live as the recognizer
  refines it. The panel explains itself if no recognition model is installed or no capture
  device is available. The panel refreshes itself automatically the moment a recognition model
  finishes downloading from the Model Catalog panel, with no manual click or application restart
  needed. The model picker disables while transcription is actively listening, so a model cannot
  be switched mid-session.
- **Model Settings** (embedded in the Text-to-Speech and Speech-to-Text panels): renders whichever
  numeric, choice, and boolean parameters the currently selected model declares, as generic
  sliders/numeric up-downs, combo boxes, and checkboxes bound two-way to the parameter's current
  value. The demo contains no per-model or per-parameter code; presenter selection is driven
  entirely by the library's `ISpeechModelParameter` concrete type. For the Text-to-Speech panel,
  the current value bag (including a selected voice, for a model such as
  `SherpaOnnxKokoroEnglishSynthesisModel` that declares one) is genuinely forwarded to
  `SpeechSynthesizerFactory.LoadAsync(...)` whenever it changes (triggering a cached-engine
  reload on the next Play) - selecting a different voice in the dropdown audibly changes the
  synthesized speech.

## Launch options

The demo accepts `--models-dir <path>`, `--mirror-url <url>` (with `--mirror-user`,
`--mirror-password` or `--mirror-bearer-token`), and `--voices-dir <path>`, matching
`speech-cli`; run `dotnet run --project src/DemaConsulting.Speech.Demo -- --help` for the list.
`--voices-dir` names an existing folder of your own `.wav`/`.flac` reference voices that is
offered to Chatterbox Turbo alongside the installed pack and the user folder under the models
root; a missing folder is rejected at start-up with an explanatory message. A value-taking option
(for example `--mirror-url --help`) followed by another launch option instead of a value is rejected as
missing its value, while a value that merely begins with a hyphen and is not a launch option is accepted.
Whitespace-only mirror URL, user, password and token values are treated as not supplied.

The mirror may also be configured through the `DEMASPEECH_MIRROR_URL` (the fallback for `--mirror-url`),
`DEMASPEECH_MIRROR_USER`, `DEMASPEECH_MIRROR_PASSWORD` and `DEMASPEECH_MIRROR_BEARER_TOKEN` environment
variables, which keep secrets
off the command line. An explicit command-line option takes precedence, and any credential option makes
the demo ignore all credential environment variables. Secret values are never printed in help or error text.
For a scripted launch, set these variables rather than passing the secret as an option.

## Diagnostics: Raw Capture Recording

The Speech-to-Text panel supports an opt-in diagnostic aid for investigating
live-microphone streaming issues: set the `DEMASPEECH_CAPTURE_DEBUG_DIR` environment variable to
a directory path before launching the demo, and each Start/Stop listening session writes the raw
microphone audio (exactly as delivered by the capture device, before the recognizer's own
resampling) to a new timestamped `capture-{yyyyMMdd-HHmmss}-{suffix}.wav` file in that
directory. The console prints the exact path written for each session. Leave the variable unset
for normal use - this has no effect and no overhead when disabled.

The model catalog contains all seven production models (see "Recognizing Speech" and
"Synthesizing Speech" above), so the Model Catalog, Text-to-Speech, and Speech-to-Text panels
populate rows for every one of them once the library composes, each with its own installation
state and download action. A model needing a bespoke, non-generic UI beyond the generic Model
Settings surface is not supported by the demo.

The demo is verified at the view-model level. Its window, layout, and interactive behavior are
not covered by automated tests, because CI runners have no display; visual verification of the
application is a manual/local activity.
