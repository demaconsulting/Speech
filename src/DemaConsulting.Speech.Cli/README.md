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
| `clean <modelId>` | Remove a downloaded model's files and its catalog entry |
| `list-devices` | List capture and/or playback audio devices (`--direction input\|output`) |
| `devices test` | Play or record a short test tone/clip (`--device <name>`, `--direction input\|output`) |
| `doctor` | Report overall environment health |
| `speak --model <id>` | Synthesize text to a device or WAV file - see [Flags](#speak-flags) below |
| `recognize --model <id>` | Recognize speech from a WAV file or the microphone - see [Flags](#recognize-flags) below |

Global options (`--models-dir`, `--verbose`/`--diagnostics`, `--silent`, `--log <file>`,
`--results <file>`, `--depth <#>`, `-v`/`--version`, `-h`/`-?`/`--help`, `--validate`) are
recognized regardless of where they appear on the command line relative to the subcommand.

### `speak` flags

One of `--text <string>`, `--file <path>`, or piped stdin supplies the text to synthesize; add
`--output <wav-path>` to write a WAV file instead of playing to a device, `--device <name>` to
pick a specific playback device, `--param key=value` (repeatable) to set model-specific synthesis
parameters, and `--no-tags` to strip inline emphasis/pause tags before synthesis.

### `recognize` flags

One of `--input <wav-path>` or `--mic` supplies the audio to recognize; add `--device <name>` to
pick a specific capture device, `--silence-timeout <seconds>` to stop microphone capture after a
period of silence, `--param key=value` (repeatable) to set model-specific recognition parameters,
`--interim`/`--final-only` to control whether interim (in-progress) results are printed, and
`--output <text-path>` to write the final recognized text to a file.

## Examples

Download a recognition model, then recognize speech from a WAV file:

```bash
speech-cli download streaming-zipformer-en-2023-06-26
speech-cli recognize --model streaming-zipformer-en-2023-06-26 --input meeting.wav
```

Speak text to a WAV file, without needing a playback device:

```bash
speech-cli download vits-piper-en_US-libritts_r-medium
speech-cli speak --model vits-piper-en_US-libritts_r-medium --text "Hello there." --output hello.wav
```

Speak text through a real playback device, selecting a specific device by name (see
`list-devices` for the exact names available on the current machine):

```bash
speech-cli speak --model vits-piper-en_US-libritts_r-medium --text "Hello there." --device "Speakers (Realtek)"
```

Recognize speech live from the microphone, stopping automatically after five seconds of silence:

```bash
speech-cli recognize --model streaming-zipformer-en-2023-06-26 --mic --silence-timeout 5
```

## Documentation

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
