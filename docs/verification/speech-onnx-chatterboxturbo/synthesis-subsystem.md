## SynthesisSubsystem Verification

### Verification Approach

The SpeechOnnxChatterboxTurbo SynthesisSubsystem is verified by automated unit tests in
`test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/SynthesisSubsystem/`. The generation loop is
tested against a fake `IChatterboxTurboNetworks`; the ONNX networks are tested against synthetic
graphs written by the test project (an encoder, an embedder using Mod and Gather, a language model
using MatMul and Concat with a key/value cache, and a decoder using Cast); the token selector and
capability profile are pure functions tested directly. End-to-end construction through
`CreateBackend` is covered in the ModelManagementSubsystem verification.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests`
- **Dependencies**: no network, no audio hardware, no real model files; tests observing ONNX
  sessions run in a non-parallel collection

### Unit-Level Test Scenarios

See the unit verification documents: `onnx-chatterboxturbo-synthesis-engine.md`,
`onnx-chatterboxturbo-networks.md`, `chatterbox-turbo-token-selector.md` and
`chatterbox-turbo-capability-profile.md`.

### Acceptance Criteria

The subsystem is accepted when every unit suite passes: prompt assembly, bounding and voice caching
in the engine; tensor contracts, thread configuration and disposal in the networks; penalized
selection; and tag rendering and segmentation in the capability profile.
