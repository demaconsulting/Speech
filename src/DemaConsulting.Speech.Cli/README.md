<!-- cspell:ignore ALSA portaudio -->
# SpeechCli

`speech-cli` is a cross-platform .NET global tool that exposes the
[DemaConsulting.Speech][link-speech-nuget] library's model management, audio device inspection,
text-to-speech, and speech-to-text capabilities directly from the command line - useful for
scripting, CI smoke checks, or trying a model without writing any code.

## Installation

```bash
dotnet tool install -g DemaConsulting.Speech.Cli
```

The tool targets .NET 10 and bundles the native inference runtime for `win-x64`, `linux-x64`,
and `osx-arm64` (the current `macos-latest` architecture) only, keeping the package a
reasonable size instead of shipping every platform's native binaries. Other platforms/
architectures are not currently supported by this package.

Once installed, run `speech-cli doctor` to check that the native audio backend, native inference
runtime, and local model store are all in a healthy state on the current machine, and
`speech-cli --help` to see every global option and subcommand, exactly as installed.

## Commands

| Command | Purpose |
| --- | --- |
| `list-models` | List known models (filter with `--role`, `--state`, `--format`) |
| `model-info <modelId>` | Show full detail for one known model |
| `download <modelId> [<modelId>...]` | Download one or more models (`--force` to re-download) |
| `uninstall <modelId>` | Remove a downloaded model's files, keeping its catalog entry |
| `clean <modelId>` | Best-effort remove leftover partial-install artifacts for a model (not a full uninstall) |
| `list-devices` | List capture and/or playback audio devices (`--direction input\|output`) |
| `devices test` | Play or record a short test tone/clip (`--device <name>`, `--direction input\|output`) |
| `doctor` | Report overall environment health |
| `speak --tts-model <id>` | Synthesize text to a device or WAV file - see [Flags](#speak-flags) below |
| `recognize --stt-model <id>` | Recognize speech from a WAV file or the microphone - see [Flags](#recognize-flags) below |
| `ask --tts-model <id> --stt-model <id>` | Speak a prompt, then listen for the reply - see [Flags](#ask-flags) below |

Global options (`--models-dir`, `--verbose`/`--diagnostics`, `--silent`, `--log <file>`,
`--results <file>`, `--depth <#>`, `-v`/`--version`, `-h`/`-?`/`--help`, `--validate`) are
recognized regardless of where they appear on the command line relative to the subcommand.

### `speak` flags

One of `--text <string>`, `--file <path>`, or piped stdin supplies the text to synthesize; add
`--output-audio <wav-path>` to write a WAV file instead of playing to a device, `--playback-device <name>` to
pick a specific playback device, `--tts-param key=value` (repeatable) to set model-specific synthesis
parameters, and `--no-tags` to strip inline emphasis/pause tags before synthesis.

### `recognize` flags

One of `--input <wav-path>` or `--mic` supplies the audio to recognize; add `--capture-device <name>` to
pick a specific capture device. In `--mic` mode, `--silence-timeout <seconds>` sets the idle period
(default 5 seconds) after which capture stops, and `--start-timeout <seconds>` sets a separate
grace period (default 8 seconds) allowed before the first result arrives; both flags apply
independently and always take effect in `--mic` mode, even when omitted, so mic mode can never
listen forever with no words uttered. `--stt-param key=value` (repeatable) sets model-specific
recognition parameters, `--interim`/`--final-only` control whether interim (in-progress) results
are printed, and `--output-text <text-path>` writes the final recognized text to a file.

### `ask` flags

`ask` speaks a prompt (from `--text <string>`, `--file <path>`, or piped stdin, resolved the same
way as `speak`) through `--tts-model <id>` on `--playback-device <name>` (or the system default),
then immediately listens on `--capture-device <name>` (or the system default) through
`--stt-model <id>`, stopping on the first final recognition result, a
`--silence-timeout <seconds>`/`--start-timeout <seconds>` timeout (same semantics and 5s/8s
defaults as `recognize --mic`), or
`Ctrl+C`. Both `--tts-model` and `--stt-model` are required. `--tts-param key=value` and
`--stt-param key=value` (each repeatable) set model-specific synthesis/recognition parameters,
and `--output-text <path>` writes the recognized reply to a file instead of stdout. `ask` has no
`--output-audio`, `--input`, `--interim`, `--final-only`, or `--no-tags` equivalent - it always
uses a real playback device and a real capture device to hold one conversational turn.

## Examples

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

### Voice Conversation Example

An AI agent can use `speak` and `ask` together to hold a two-way voice conversation with a
person through this CLI: `speak` for a one-way statement, `ask` when a reply is expected.

```bash
# Make a statement
speech-cli speak --tts-model vits-piper-en_US-libritts_r-medium --text "Backup finished successfully."

# Ask a question and read the reply, allowing up to 20 seconds to start speaking and
# ending the turn after 1.5 seconds of silence
speech-cli ask --tts-model vits-piper-en_US-libritts_r-medium --stt-model streaming-zipformer-en-2023-06-26 \
  --text "Do you want me to continue?" --start-timeout 20 --silence-timeout 1.5
```

This `speak`/`ask` pairing is the intended integration pattern for an AI agent holding a two-way
voice conversation with a person through this CLI.

See the [full user guide][link-user-guide] and [repository][link-repository] for the underlying
library's API, model catalog, and Natural Language Audio Tag vocabulary.

## License

Copyright (c) DEMA Consulting. Licensed under the MIT License. See
[LICENSE][link-license] for details.

<!-- Link References -->
[link-speech-nuget]: https://www.nuget.org/packages/DemaConsulting.Speech
[link-user-guide]: https://github.com/demaconsulting/Speech/blob/main/docs/user_guide/introduction.md
[link-repository]: https://github.com/demaconsulting/Speech
[link-license]: https://github.com/demaconsulting/Speech/blob/main/LICENSE
