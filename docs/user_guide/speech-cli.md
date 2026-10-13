## SpeechCli

`speech-cli` is a cross-platform .NET global tool, packaged as `DemaConsulting.Speech.Cli`, that
exposes the library's model management, audio device inspection, text-to-speech, and
speech-to-text capabilities directly from the command line - useful for scripting, CI smoke
checks, or trying a model without writing any code. It is a sibling application to the library,
not a wrapper around the demo; it is separately packaged and versioned.

### Installing SpeechCli

```bash
dotnet tool install -g DemaConsulting.Speech.Cli
```

The tool targets .NET 10 and bundles the native inference runtime for `win-x64`, `linux-x64`,
and `osx-arm64` (the current `macos-latest` architecture) only, keeping the package a reasonable
size instead of shipping every platform's native binaries.

Once installed, the `speech-cli` command is available on the `PATH`. Run `speech-cli doctor` any
time to check that the native audio backend, native inference runtime, and model store are all in
a healthy state on the current machine.

### Command Overview

| Command | Purpose |
| --- | --- |
| `list-models` | List known models, optionally filtered by role or download state |
| `model-info <modelId>` | Show full detail for one known model |
| `download <modelId> [<modelId>...]` | Download one or more models into the local model store |
| `uninstall <modelId>` | Remove a downloaded model's files, keeping its catalog entry |
| `clean <modelId>` | Best-effort remove leftover partial-install artifacts for a model (not a full uninstall) |
| `list-voices` | List reference voices (id, source) for the voice-cloning model, as a table or JSON |
| `voices packs\|install [--no-emotions]\|uninstall` | List, download, or remove the optional reference-voice pack |
| `list-devices` | List capture and/or playback audio devices |
| `devices test` | Play or record a short test tone/clip on a chosen device |
| `doctor` | Report overall environment health (audio, native runtimes, model store) |
| `speak` | Synthesize text to a real playback device or a WAV file |
| `recognize` | Recognize speech from a WAV file or the microphone |
| `ask` | Speak a prompt, then listen for the reply |

Every command's exact flags are shown by `speech-cli --help`, which always reflects the installed
version - run it locally rather than relying solely on the summary above, which is illustrative
only.

### Reference voices (Chatterbox Turbo)

The `chatterbox-turbo-onnx-en` text-to-speech model clones a *reference voice*, so it needs at
least one voice to speak. Voices come from three places, all listed by `list-voices`:

- the optional **voice pack** `voice-zero-1.0` (CC0-licensed, about 570 MiB to download; about 44 MiB
  installed for the base voices, about 572 MiB with the 13 emotion variants per voice, which are
  included by default): `speech-cli voices install voice-zero-1.0` (add `--no-emotions` for the base
  voices only; installing with emotions over a voices-only install removes and downloads the pack
  again; `voices packs` shows its sizes and state, `voices uninstall voice-zero-1.0` removes it;
  `list-voices` shows a Moods column). Installation honors `--models-dir` and
  `--mirror-*` (the mirror serves `{mirror-url}/voice-zero-1.0/voice-zero-1.0.zip`)
- the **user folder** `<models-dir>/reference-voices/user`: drop `.wav` or `.flac` files here
- any folder given with `--voices-dir <path>` (must exist)

Each voice file must be 1-30 seconds of clean speech; the file name (without extension) is the
voice id. Choose a voice with `--tts-param voice=<id>`:

```bash
speech-cli voices install voice-zero-1.0
speech-cli list-voices
speech-cli speak --tts-model chatterbox-turbo-onnx-en --tts-param voice=<id> --text "Hello"
```

Newly installed voices appear on the next invocation. Voice files in `--voices-dir` are decoded
only when a voice is used, so an undecodable file does not break other commands: `list-voices` still
runs and lists it, and synthesizing with that voice fails with an error naming the file.

> **Consent:** only clone voices you have permission to use. `list-voices`, `voices packs`,
> `voices install`, and `--help` all repeat this note.

### Worked Examples

Download a recognition model, then recognize speech from a WAV file:

```bash
speech-cli download streaming-zipformer-en-2023-06-26
speech-cli recognize --stt-model streaming-zipformer-en-2023-06-26 --input meeting.wav
```

Speak text to a WAV file, without needing a playback device:

```bash
speech-cli download vits-piper-en_US-libritts_r-medium
speech-cli speak --tts-model vits-piper-en_US-libritts_r-medium --text "Hello there." --output-audio hello.wav
```

Speak text through a real playback device, selecting a specific device by name (see
`list-devices` for the exact names available on the current machine):

```bash
speech-cli speak --tts-model vits-piper-en_US-libritts_r-medium --text "Hello there." --playback-device "Speakers (Realtek)"
```

Recognize speech live from the microphone, stopping automatically after five seconds of silence
(and giving up to ten seconds to start speaking):

```bash
speech-cli recognize --stt-model streaming-zipformer-en-2023-06-26 --mic --silence-timeout 5 --start-timeout 10
```

`--silence-timeout` and `--start-timeout` apply only to `--mic`; when combined with `--input <wav-path>` they are
accepted and ignored, because a file ends on its own.

Speak a prompt and listen for the reply in one invocation, giving up to 20 seconds to start
speaking and ending capture after 1.5 seconds of silence - the intended pattern for an AI agent
holding a two-way voice conversation with a person through this CLI. `ask` pre-warms (constructs
and loads) its STT recognizer concurrently with speaking the prompt, rather than only afterward,
so the reply can be heard with minimal added latency; see "Hot TTS/STT: Reusing an Instance
Across Turns" above for the same low-latency create-once/reuse-many pattern applied inside a
long-lived host process:

```bash
speech-cli ask --tts-model vits-piper-en_US-libritts_r-medium --stt-model streaming-zipformer-en-2023-06-26 \
  --text "Do you want me to continue?" --start-timeout 20 --silence-timeout 1.5
```
