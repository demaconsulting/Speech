### SynthesisBackend (ISynthesisBackend, ISynthesisBackendFactory, DefaultSynthesisBackendFactory)

#### Verification Approach

The seam interfaces are verified indirectly: every `SynthesisSession` test drives a deterministic
fake `ISynthesisBackend` (recording each `Generate` call and returning a fixed amount of audio),
proving the whole synthesis pipeline runs with no downloaded model and no native speech-inference
runtime. `DefaultSynthesisBackendFactory` is verified directly against a test model that returns a
known backend from `CreateBackend`, plus invalid-argument cases. The optional `IAnnotatedSynthesisBackend` seam is
verified through the real session against a fake that implements it
(`FakeAnnotatedSynthesisEngine`, which records plain `Generate` calls and `GenerateAnnotated`
calls separately) and against the legacy fake.

The real, native-backed `ISynthesisBackend` implementations live in sibling libraries
(for example `SpeechSherpa`); their verification is recorded in those libraries' own verification
documents.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: None - no model files, no native runtime, no audio hardware
- **Test doubles**: Fake `ISynthesisBackend`/`ISynthesisBackendFactory` implementations, a fake
  `IAnnotatedSynthesisBackend`, and a test
  `ISynthesisModel`

#### Acceptance Criteria

The unit is considered verified when the synthesis pipeline can be exercised end to end through a
fake backend, `DefaultSynthesisBackendFactory.Create` returns exactly the backend the model's own
`CreateBackend` produced, and it rejects a null model or a null/empty directory. A backend that
implements `IAnnotatedSynthesisBackend` receives a segment's annotations with its text; a backend
that does not receives exactly the text it would have received without text analysis.

#### Test Scenarios

##### Backend Seam Is Mockable

**Stimulus**: A session is constructed over a fake backend and asked to synthesize plain text.

**Expected**: An audio segment is produced without any model or native runtime.

**Tests** (`test/DemaConsulting.Speech.Tests/SynthesisSubsystem`):
`SynthesisSession_SynthesizeAsync_PlainText_YieldsAudioSegment`,
`SpeechSynthesizerFactory_LoadAsync_ModelInstalled_ReturnsRealEngine`

##### Model-Owned Backend Construction

**Stimulus**: `DefaultSynthesisBackendFactory.Create` is called with a valid model and directory,
then with a null model and with a null or empty directory.

**Expected**: The valid call returns the backend the model's `CreateBackend` produced; the invalid
calls throw `ArgumentNullException` / `ArgumentException`.

**Tests** (`DefaultSynthesisBackendFactoryTests.cs`):
`DefaultSynthesisBackendFactory_Create_ValidArguments_ReturnsModelCreatedBackend`,
`DefaultSynthesisBackendFactory_Create_InvalidArguments_Throws`

##### Optional Annotated Backend Seam

**Stimulus**: A session synthesizes text whose segments carry annotations, once against a fake that
implements `IAnnotatedSynthesisBackend` and once against a legacy fake that implements only
`ISynthesisBackend`, with the same model, analyzer, and text.

**Expected**: The annotated fake receives `GenerateAnnotated` calls whose annotations are relative
to the received segment text; the legacy fake receives plain `Generate` calls whose texts are
identical to the annotated fake's segment texts and to a run with no analyzer, proving the seam
is optional and a backend that does not implement it is unchanged.

**Tests** (`SynthesisSessionTests.cs`):
`SynthesisSession_SynthesizeAsync_AnnotatedBackend_ReceivesAnnotationsWithSegmentText`,
`SynthesisSession_SynthesizeAsync_LegacyBackendWithAnalyzer_ReceivesPlainTextIdenticalToUnannotatedRun`
