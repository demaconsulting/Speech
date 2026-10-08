### SherpaOnnxSynthesisEngine

#### Verification Approach

`SherpaOnnxSynthesisEngine` wraps sherpa-onnx's offline text-to-speech synthesizer as the real
implementation of the Speech library's internal `ISynthesisBackend` seam: its constructor loads
the native synthesizer from an `OfflineTtsConfig`, `SampleRate` reports the synthesizer's declared
output rate, `Generate` produces one audio buffer for a given text, speed, and speaker id, and
`Dispose` releases the native synthesizer idempotently. Every one of those operations requires
the real native runtime and a real, installed synthesis model; a mocked native layer would prove
nothing about this unit's purpose. The intended verification approach is therefore direct,
self-skipping tests against an installed VITS or Kokoro model, mirroring
`SherpaOnnxRecognitionEngineTests`.

**No such test exists today.** There is currently no unit test for `SherpaOnnxSynthesisEngine`
anywhere in the repository. This gap pre-dates the SpeechSherpa package split - the unit was
equally untested while it lived in the Speech library - and the split carried it forward
unchanged; it was not introduced by the move. It is disclosed here deliberately rather than
hidden.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Sherpa.Tests` (no test class for this unit exists yet)
- **Dependencies**: A future test would require the real native sherpa-onnx runtime and a real,
  installed synthesis model, self-skipping when absent

#### Acceptance Criteria

**No automated test currently verifies this unit, so no acceptance criterion is claimed as met.**
This is a pre-existing coverage gap carried forward by the package split, not introduced by it.
The surrounding evidence that limits the risk of this gap is:

- All synthesis policy above the seam is verified in the Speech library against a fake backend
  (see _Speech SynthesisSubsystem Verification_)
- Each synthesis model's `OfflineTtsConfig` construction is verified by its `BuildEngineConfig`
  tests (see _SpeechSherpa ModelManagementSubsystem Verification_)
- Real, non-silent audio through this unit was proven by one-time manual spikes recorded in the
  `SherpaOnnxVitsLibriTtsEnglishSynthesisModel` and `SherpaOnnxKokoroEnglishSynthesisModel`
  verification documents

When a test is added, the unit should be considered verified when construction from a valid
configuration reports the model's declared `SampleRate`, `Generate` returns non-empty audio for
non-empty text, `Generate` throws `ArgumentNullException` for null text and
`ObjectDisposedException` after disposal, and a second `Dispose` call is a no-op.

#### Test Scenarios

None - no automated test exists for this unit (pre-existing gap; see Acceptance Criteria).
