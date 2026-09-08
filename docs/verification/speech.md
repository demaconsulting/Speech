# System Verification Design

This document describes the system-level verification strategy for the Speech.

## Verification Approach

The Speech system is verified through deterministic system-level integration tests that compose
`AudioDeviceFactory` with controlled fake PortAudio environments and optional diagnostics sinks,
compose model storage/download/catalog over a scratch store root and a fake download client,
compose `SpeechRecognizerFactory` over an installed test model, a substitute capture device, and a
fake recognition engine, and compose `SpeechSynthesizerFactory`/`AudioTagParser` over an installed
test model, a substitute playback device, and a fake synthesis engine. This proves the public
composition surface, fallback behavior, diagnostics reporting, the streaming-recognition pipeline,
the Natural Language Audio Tag vocabulary/parser, and the chunked streaming-synthesis pipeline
without requiring physical audio hardware, network access, a downloaded speech model, or a native
speech-inference runtime in CI.

Automated coverage **does not** extend to actually opening a real microphone or speaker and
moving audio end to end through hardware, nor to recognizing or synthesizing real speech through
a real model. Those remain manual/local verification activities because CI runners cannot
guarantee audio hardware. The library's compiled-in catalog now ships four real, production
models - `SherpaOnnxZipformerEnRecognitionModel`, `SherpaOnnxNemotronStreamingEnRecognitionModel`,
`SherpaOnnxVitsLibriTtsEnglishSynthesisModel`, and `SherpaOnnxKokoroEnglishSynthesisModel` - but
automated system tests deliberately exercise a deterministic test model and a fake recognition
or synthesis engine instead of a real model, so CI never depends on a real, multi-hundred-megabyte
download or the native sherpa-onnx runtime.

System tests reside in `SpeechTests.cs` within the `DemaConsulting.Speech.Tests` project, with the
Natural Language Audio Tag and streaming-synthesis scenarios additionally proven by
`AudioTagParserTests.cs` and `SherpaOnnxSpeechSynthesizerTests.cs` in the same project.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services and no guaranteed physical audio hardware
- **Isolation**: Each test method constructs its own deterministic `PortAudioEnvironment`, and
  each model-storage/download/catalog test roots its `SpeechModelStore` in a fresh temporary
  directory so no test ever reads or writes a developer's real installed-model store

## External Interface Simulation

The system simulates the PortAudio runtime through an internal fake `IPortAudioApi` exposed via
`PortAudioEnvironment`. This allows system tests to exercise the public `AudioDeviceFactory`
composition surface with controlled host-API and device catalogs while remaining deterministic
on every CI runner.

## System-Level Test Scenarios

### Integration: PortAudio Initialized Returns Real Devices

**Test**: `Speech_SystemIntegration_PortAudioInitialized_FactoryReturnsRealDevices`

Verifies that the public composition surface returns real PortAudio-backed device abstractions
when PortAudio initialization succeeds.

### Integration: Diagnostics Sink Receives Structural Events

**Test**: `Speech_SystemIntegration_DiagnosticsSink_ReceivesStructuralEvents`

Verifies that a host-supplied diagnostics sink receives structural selection events reported
during audio-device composition.

### Integration: No Diagnostics Supplied Uses the Null Sink Safely

**Test**: `Speech_SystemIntegration_NoDiagnosticsSupplied_UsesNullSpeechDiagnosticsSafely`

Verifies that omitting the diagnostics sink still composes safely through
`NullSpeechDiagnostics`.

### Integration: PortAudio Initialization Failure Returns Unavailable Devices

**Test**: `Speech_SystemIntegration_PortAudioInitializationFails_FactoryReturnsUnavailableDevices`

Verifies that PortAudio initialization failure degrades composition to the honest unavailable
fallback devices rather than throwing.

### Validation: No Resolvable Capture Device Throws on Start

**Test**: `Speech_SystemValidation_NoResolvableCaptureDevice_StartThrowsAudioDeviceUnavailableException`

Verifies that a capture device the factory's own default probe could not resolve (an
initialized environment whose preferred host API has no capture-capable devices) reports
`IsAvailable = false` and throws the documented exception when a caller attempts to start it -
consistent with `AudioDeviceFactory` consulting its probe before constructing a real device.

### Integration: Streaming Recognition Produces Results from Captured Audio

**Test**: `Speech_SystemIntegration_StreamingRecognition_CapturedAudioProducesRecognitionResults`

Verifies the full public streaming-recognition surface end to end: composing a recognizer for an
installed model and an available capture device, streaming one captured block through audio-format
conversion into the recognition engine, and surfacing the resulting provisional and final text
through the recognizer's result event. The recognition engine is a deterministic test double, so
this scenario proves the composed pipeline rather than speech-recognition accuracy.

### Integration: Model Download Verifies and Atomically Installs the Model

**Test**: `Speech_SystemIntegration_ModelDownload_VerifiesAndAtomicallyInstallsModel`

Verifies `SpeechModelDownloader` end to end over a scratch store root and a fake download client
serving a known payload: a declared file is fetched, its SHA-256 checksum is verified, and it is
atomically installed and reported installed rather than left partial or corrupted.

### Integration: Model Catalog Enumerates and Tracks Download State

**Test**: `Speech_SystemIntegration_ModelCatalog_EnumeratesAndTracksDownloadState`

Verifies `SpeechModelCatalog` end to end over a scratch store root, one known fake model, and a
fake download client: `Enumerate()` reports the model's install state, and the state transitions
to `SpeechModelState.Downloaded` once `DownloadAsync` verifies and atomically installs the model.

### Integration: Streaming Synthesis Produces Played Audio from Text

**Test**: `Speech_SystemIntegration_StreamingSynthesis_TextProducesPlayedAudio`

Verifies the full public streaming-synthesis surface end to end: composing a synthesizer for an
installed model and an available playback device, speaking one plain-text utterance, and
confirming the playback device is started, receives at least one written block of audio, and is
stopped. The synthesis engine is a deterministic test double, so this scenario proves the composed
chunked pipeline rather than speech-synthesis audio quality.

### Unit: Every Documented Audio Tag Alias Resolves to Its Canonical Tag Span

**Test**: `AudioTagParser_Parse_EveryDocumentedAlias_ResolvesToItsCanonicalTagSpan`

Verifies, for every alias in the closed Natural Language Audio Tag vocabulary, that
`AudioTagParser.Parse` resolves the bracketed alias to its canonical tag span, proving the
model-independent Layer 1 parser recognizes the complete documented vocabulary.

### Unit: Unknown Bracketed Word Passes Through as Literal Text

**Test**: `AudioTagParser_Parse_UnknownBracketedWord_PassesThroughAsLiteralText`

Verifies that a bracketed word outside the closed vocabulary is passed through as plain narration
text rather than dropped or rejected, proving the "never worse than plain narration" guarantee for
unrecognized or malformed bracket content.

### Unit: Plain Text Synthesis Yields an Audio Segment

**Test**: `SynthesizeStreamAsync_PlainText_YieldsAudioSegment`

Verifies that `SherpaOnnxSpeechSynthesizer.SynthesizeStreamAsync` yields at least one audio segment
for plain text with no audio tags, proving the Layer 2 rendering and chunked synthesis pipeline
produce audio for the simplest input.

### Unit: Ordered Segments Start, Write in Order, and Stop Playback

**Test**: `PlayStreamAsync_OrderedSegments_StartsWritesInOrderAndStops`

Verifies that `SherpaOnnxSpeechSynthesizer.PlayStreamAsync` starts the playback device, writes a
mono 16 kHz silence segment followed by an audio segment to the device in order, and stops the
device once playback completes.

## Acceptance Criteria

A system-level test run passes when all scenarios above pass without unexpected exceptions and
when the automated verification boundary remains honest: only deterministic seam-driven behavior
is claimed as automated coverage, while physical device I/O is explicitly left to manual/local
verification.
