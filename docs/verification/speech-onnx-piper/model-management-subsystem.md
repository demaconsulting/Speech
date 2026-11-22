## ModelManagementSubsystem Verification

### Verification Approach

The SpeechOnnxPiper ModelManagementSubsystem is verified by deterministic tests in
`test/DemaConsulting.Speech.Onnx.Piper.Tests` that never touch the network or the real voice:

- **`SpeechModelCatalogPiperExtensions`** is verified against a real Speech `SpeechModelCatalog` in
  `SpeechModelCatalogPiperExtensionsTests.cs`: it registers exactly the one shipped model with the Synthesis role,
  returns the same catalog (also when given a preferred provider name) and rejects a null catalog. This subsystem's
  documents fold this extension class in as its own review unit (there is no separate unit verification file), following
  the convention of the SpeechSherpa and SpeechOnnxKokoro systems
- **`OnnxPiperLibriTtsEnSynthesisModel`** (with its bundle install) and **`PiperVoiceConfig`** are verified
  by their own unit tests (see their verification documents)
- **The units together** are verified in `ModelManagementSubsystem/ModelManagementSubsystemTests.cs` over a tiny
  stand-in ONNX graph that echoes what it is fed, so that the whole path from a downloaded bundle to a speaking voice
  is exercised: rejection of a tampered bundle and creation of the backend from an installed directory, the speaker
  that the model resolves reaching the loaded voice, rejection of a voice configuration before the graph is loaded (the
  graph file in that test is not loadable, so a configuration check that came after the session creation would fail with
  another error), and the fallback of an unavailable preferred provider to the CPU for a model registered through the
  catalog

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the library
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Onnx.Piper.Tests`
- **Isolation**: each test uses its own scratch directory under `Path.GetTempPath()`; no test needs network access or
  the real voice. The tampered bundles and the stand-in graph are built by `PiperTestFixtures`

### Acceptance Criteria

The subsystem tests pass when: `AddPiperModels()` registers exactly one Synthesis-role model with the identifier
`piper-onnx-libritts-r-en-us-medium`, returns the same catalog and throws `ArgumentNullException` for a null catalog; a
bundle whose vendor bytes differ from the compiled pins fails the installation with `InvalidDataException`, and a
backend created at 22,050 Hz from an installed voice produces the framing ids; the speaker value 7 resolves to speaker 7
and runs the voice with speaker 7, while 5000 and a missing value resolve to speaker 0; a configuration with one
speaker, with a 16,000 Hz sample rate, with a phoneme type other than espeak or without a phoneme map fails
`CreateBackend` with `InvalidDataException` although the graph beside it is not loadable; and a model registered with an
unavailable provider name creates a working backend.

### Test Scenarios

#### Catalog registration

**Tests**: `AddPiperModels_Called_RegistersExpectedSingleSynthesisModel`,
`AddPiperModels_Called_ReturnsSameCatalogInstance`, `AddPiperModels_NullCatalog_ThrowsArgumentNullException`

The catalog's enumeration holds one descriptor with the model's identifier and the Synthesis role, and the registered
model is the Piper model; the returned catalog is the one called on, also with `["NotARealExecutionProvider"]` as the
provider names; a null catalog throws.

#### Download and installation

**Tests**: `ModelManagementSubsystem_Install_TamperedBundle_IsRejected`,
`ModelManagementSubsystem_CreateBackend_InstalledVoice_Speaks`

A staged bundle zip of the model's bundle name, whose `model.json` declares the three compiled pins but whose vendor
entries hold other bytes, fails `InstallAsync` with `InvalidDataException`. An installed voice (the stand-in graph and
the configuration at the installed root) creates a backend at 22,050 Hz, and `Generate("Hello there.")` feeds ids that
start with the begin id 1, end with the end id 2 and hold more than the framing.

#### Speaker selection

**Test**: `ModelManagementSubsystem_ResolvedSpeaker_ReachesTheLoadedVoice`

The speaker resolved from `7.0` runs the voice with speaker 7, and the speaker resolved from `5000.0` and from a null
value runs it with speaker 0.

#### Configuration validation

**Test**: `ModelManagementSubsystem_CreateBackend_VoiceOfAnotherShapeOrBrokenConfiguration_FailsBeforeTheGraphIsLoaded`

Four configurations (one speaker, another sample rate, a phoneme type `text`, no phoneme map) each throw
`InvalidDataException` while the graph file is garbage.

#### Provider selection

**Test**: `ModelManagementSubsystem_CreateBackend_UnavailablePreferredProvider_FallsBackToCpuAndSynthesizes`

The model that `AddPiperModels(["NotARealExecutionProvider"])` registered creates a backend at 22,050 Hz that
synthesizes.

See each unit's own verification document for its detailed scenarios: `onnx-piper-libri-tts-en-synthesis-model.md` and
`piper-voice-config.md`; see also *SpeechOnnxPiper System Verification*.
