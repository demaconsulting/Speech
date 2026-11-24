# ![Speech banner: the Speech name with the tagline 'Local speech for .NET: text to voice, voice to text.' and the text 'Reads English text and audio tags; speaks to speakers or WAV files, and streams recognized text from a mic.' Shown with the C# call 'await engine.SpeakAsync(speakers, "Hello, world.");' and the note '.NET 8–11 · Windows, Linux, macOS'. English text, Natural Language Audio Tags, microphone audio, and downloaded ONNX models flow into the Speech .NET library, and arrows lead out to what the library produces: spoken audio, the phonemes of a heteronym such as read in the past tense, live partial and final recognized text, and WAV files][hero]

[![GitHub forks][badge-forks]][link-forks]
[![GitHub stars][badge-stars]][link-stars]
[![GitHub contributors][badge-contributors]][link-contributors]
[![License][badge-license]][link-license]
[![Build][badge-build]][link-build]
[![Quality Gate][badge-quality]][link-quality]
[![Security][badge-security]][link-security]
[![NuGet][badge-nuget]][link-nuget]

<!-- IMPORTANT: All links in this file must be absolute URLs.
     This file is distributed in packages and relative links will not resolve. -->

## Overview

DemaConsulting.Speech is a cross-platform .NET library for local, offline text-to-speech and speech-to-text in desktop
applications. Models are downloaded on demand into a per-user store and run on the machine, with no cloud service. The
English voices speak through a DEMA English front end that reads numbers, dates and heteronyms in context and
pronounces words from the CMU Pronouncing Dictionary, with a trained letter-to-sound model for words it lacks.

## Features

- 🔊 **Text-to-speech** - Piper (904 speakers) and Kokoro (29 voices) English voices on ONNX Runtime
- 🔤 **English front end** - numbers, dates, units, web addresses and heteronyms read in context
- 🗣️ **Pronunciation** - CMU Pronouncing Dictionary lexicon with a trained letter-to-sound fallback
- 🎙️ **Speech-to-text** - streaming recognition with NVIDIA Nemotron 3.5 on ONNX Runtime
- 🧬 **Voice cloning** - Chatterbox Turbo speaks in the voice of a short reference clip
- 🏷️ **Audio tags** - Natural Language Audio Tags such as `[whispers]` and `[short pause]`
- 📦 **Model management** - on-demand download with SHA-256 verification
- 🧩 **Audio devices** - mockable, cross-platform devices that degrade gracefully without hardware
- 💻 **Tools** - the `speech-cli` command-line tool and an Avalonia demo application
- 🧵 **Wide support** - libraries for .NET 8, 9, 10 and 11 on Windows, Linux and macOS
- ✅ [**Continuous Compliance**][link-continuous-compliance] - evidence generated on every CI run

## Installation

Add the Piper voice package to your project. It brings the core library, the English front end and ONNX Runtime with
it, and it is published only in pre-release versions:

```bash
dotnet add package DemaConsulting.Speech.Onnx.Piper --prerelease
```

To use the library from the command line instead, install the `speech-cli` .NET tool (it targets .NET 10):

```bash
dotnet tool install -g DemaConsulting.Speech.Cli --prerelease
```

If the newest version on NuGet does not contain these packages yet, build from a clone of this repository instead:
reference `src/DemaConsulting.Speech.Onnx.Piper/DemaConsulting.Speech.Onnx.Piper.csproj` from your project, and run
the tool with `dotnet run --project src/DemaConsulting.Speech.Cli --` followed by its arguments.

## Usage

This program downloads the Piper voice on its first run (it is SHA-256 verified and reused afterwards) and speaks a
sentence through the default speakers:

```csharp
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Piper;
using DemaConsulting.Speech.SynthesisSubsystem;

using var catalog = new SpeechModelCatalog().AddPiperModels();
var model = (ISynthesisModel)catalog.Enumerate().First(d => d.Role == SpeechModelRole.Synthesis).Model;
await catalog.DownloadAsync(model.Id);

var speakers = new AudioDeviceFactory().CreatePlaybackDevice(AudioDeviceSelection.SystemDefault, model.PreferredAudioFormat);
await using var engine = await SpeechSynthesizerFactory.LoadAsync(model, catalog);
if (engine.IsAvailable)
{
    await engine.SpeakAsync(speakers, "I read the record on May 5th.");
}
```

`LoadAsync` does not throw for an ordinary machine state such as a missing model or no speakers; it reports
`IsAvailable` as `false` instead. For the Kokoro voices, use the `DemaConsulting.Speech.Onnx.Kokoro` package and
`.AddKokoroModels()`.

The same sentence from the command line, written to a WAV file (leave out `--output-audio` to play it):

```bash
speech-cli download piper-onnx-libritts-r-en-us-medium
speech-cli speak --tts-model piper-onnx-libritts-r-en-us-medium --text "I read the record on May 5th." --output-audio hello.wav
```

Run `speech-cli --help` for every command, including speech recognition, model and device management, and voices.

## Building

Every project script requires an explicit scope: run with no arguments it prints its usage text and
exits 1. Run `pwsh ./build.ps1 -Help` (likewise `lint.ps1` and `fix.ps1`) for the switches.

```pwsh
pwsh ./build.ps1 -All       # full build, pack verification, and all tests
pwsh ./build.ps1 -Changed   # only what your changes affect, while iterating
pwsh ./lint.ps1 -All        # all lint checks (what CI runs)
pwsh ./fix.ps1 -Changed     # auto-fix the changed file types
```

The demo application runs with `dotnet run --project src/DemaConsulting.Speech.Demo`.

## More Information

The generated documents are attached as PDFs to every release on the [releases page][link-releases]: the build notes,
the user guide, the technical overview, the software design, the verification design, the requirements with the trace
matrix, and the code quality and review reports. Their sources are in the repository:

- [User Guide][link-user-guide] - installation, models, speech-to-text, text-to-speech, audio tags and the CLI
- [Technical Overview][link-technical-overview] - the English front end, the listening-improvement loop and the
  accuracy figures
- [Software Design][link-design] - the structure of the libraries, the tool and the demo
- [speech-cli README][link-cli-readme] - the command-line tool and its options
- [Proofreader][link-proofreader] - the developer tool for listening to synthesized text and flagging errors

## Contributing

Contributions are welcome. See [CONTRIBUTING.md][link-contributing] for development setup, coding standards, and the
pull request process.

## License

Copyright (c) DEMA Consulting. The code is licensed under the MIT License; see [LICENSE][link-license]. Packages that
embed third-party data, such as the English front end, carry their own `PACKAGE-LICENSE.md` and
`THIRD-PARTY-NOTICES.md`, and every downloaded model has its own license, which it reports through the API.

The ONNX models (Kokoro, Nemotron, Chatterbox Turbo and the Piper voice) are redistributed, unmodified, as zip bundles
on the project's [`models` release][link-models-release]. Each bundle carries the model's own `LICENSE`, a `NOTICE` with
the required attribution and a `README.txt` describing its origin, and is installed with the model. The models keep
their own licenses: Kokoro is Apache-2.0, the Nemotron weights are OpenMDW-1.1 (its ONNX conversion MIT), and Chatterbox
Turbo is MIT (Copyright (c) 2025 Resemble AI).

The Piper voice is licensed CC BY 4.0, as its model card declares, and requires attribution of the LibriTTS-R corpus
(Koizumi et al., 2023; CC BY 4.0; [OpenSLR 141](https://www.openslr.org/141/)) and the Piper project (Rhasspy). Its
model card also states that it was fine-tuned from the English Lessac medium voice, which was trained on the Blizzard
Challenge 2013 Lessac data; that data is licensed for non-commercial research use only. Review the upstream terms and
decide whether the voice fits your use.

By contributing to this project, you agree that your contributions will be licensed under the MIT License.

## Support

- [Report a bug or request a feature][link-issues]

<!-- Badge References -->
[badge-forks]: https://img.shields.io/github/forks/demaconsulting/Speech?style=plastic
[badge-stars]: https://img.shields.io/github/stars/demaconsulting/Speech?style=plastic
[badge-contributors]: https://img.shields.io/github/contributors/demaconsulting/Speech?style=plastic
[badge-license]: https://img.shields.io/github/license/demaconsulting/Speech?style=plastic
[badge-build]:
  https://img.shields.io/github/actions/workflow/status/demaconsulting/Speech/build_on_push.yaml?style=plastic
[badge-quality]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_Speech&metric=alert_status
[badge-security]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_Speech&metric=security_rating
[badge-nuget]: https://img.shields.io/nuget/v/DemaConsulting.Speech?style=plastic

<!-- Link References -->
[link-forks]: https://github.com/demaconsulting/Speech/network/members
[link-stars]: https://github.com/demaconsulting/Speech/stargazers
[link-contributors]: https://github.com/demaconsulting/Speech/graphs/contributors
[link-license]: https://github.com/demaconsulting/Speech/blob/main/LICENSE
[link-build]: https://github.com/demaconsulting/Speech/actions/workflows/build_on_push.yaml
[link-quality]: https://sonarcloud.io/dashboard?id=demaconsulting_Speech
[link-security]: https://sonarcloud.io/dashboard?id=demaconsulting_Speech
[link-nuget]: https://www.nuget.org/packages/DemaConsulting.Speech
[link-continuous-compliance]: https://github.com/demaconsulting/ContinuousCompliance
[link-contributing]: https://github.com/demaconsulting/Speech/blob/main/CONTRIBUTING.md
[link-releases]: https://github.com/demaconsulting/Speech/releases
[link-models-release]: https://github.com/demaconsulting/Speech/releases/tag/models
[link-user-guide]: https://github.com/demaconsulting/Speech/blob/main/docs/user_guide/introduction.md
[link-technical-overview]: https://github.com/demaconsulting/Speech/blob/main/docs/technical_overview/introduction.md
[link-design]: https://github.com/demaconsulting/Speech/blob/main/docs/design/introduction.md
[link-cli-readme]: https://github.com/demaconsulting/Speech/blob/main/src/DemaConsulting.Speech.Cli/README.md
[link-proofreader]: https://github.com/demaconsulting/Speech/blob/main/tools/DemaConsulting.Speech.Proofreader/README.md
[link-issues]: https://github.com/demaconsulting/Speech/issues

<!-- Image References -->
[hero]: https://raw.githubusercontent.com/demaconsulting/Speech/main/docs/images/hero.png
