### Layer2Rendering (IModelCapabilityProfile and DefaultModelCapabilityProfile)

#### Verification Approach

`DefaultModelCapabilityProfile` is a pure function from a Layer 1 span list and a model to a
`SpeechPlan`, so it is verified directly with a `StubSpeechModel` test double whose audio-tag
support level and parameters are set per test. Each test builds the span list by hand (or via
`AudioTagParser.Parse`) and asserts on the resulting segments' text, silence durations and
parameter overrides.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: None - no model files, no native runtime, no audio hardware
- **Test doubles**: `StubSpeechModel`

#### Acceptance Criteria

The unit is considered verified when a pause tag always yields a silence segment (long strictly
longer than short) for every support level; a native model receives the canonical bracket text; a
parameter-mapped model gets a speed/volume override for a mapped tag and a silent strip for an
unmapped one; a `None` model has every non-pause tag stripped; plain text is chunked into one
segment per chunk; a chunk ending in an ellipsis gets a longer post-silence than an ordinary
sentence end; and a null span list or model is rejected.

#### Test Scenarios

All tests are in `test/DemaConsulting.Speech.Tests/SynthesisSubsystem/DefaultModelCapabilityProfileTests.cs`.

##### Pauses Always Render As Silence

**Stimulus**: A short and a long pause tag are rendered for models of every support level.

**Expected**: A pure-silence segment, with the long pause strictly longer.

**Tests**: `DefaultModelCapabilityProfile_Render_ShortPause_AlwaysRendersAsSilenceSegment`,
`DefaultModelCapabilityProfile_Render_LongPause_RendersLongerSilenceThanShortPause`

##### Native, Parameter-Mapped, and Stripped Tags

**Stimulus**: Tags are rendered against `Native`, `ParameterMapped` and `None` models.

**Expected**: Native passes the canonical bracket text through; parameter-mapped maps `[fast]` to
a speed override and strips an emotion tag without an override; `None` strips every non-pause tag.

**Tests**: `DefaultModelCapabilityProfile_Render_NativeSupport_PassesThroughCanonicalBracketText`,
`DefaultModelCapabilityProfile_Render_ParameterMappedSupport_FastTag_MapsToSpeedParameter`,
`DefaultModelCapabilityProfile_Render_ParameterMappedSupport_EmotionTag_StripsWithNoOverride`,
`DefaultModelCapabilityProfile_Render_NoneSupport_StripsAllTags`

##### Plain Text Chunking and Ellipsis Pause

**Stimulus**: Multi-sentence plain text, and text with a trailing adjacent or spaced ellipsis.

**Expected**: One segment per chunk; an ellipsis-ending chunk carries the longer post-silence.

**Tests**: `DefaultModelCapabilityProfile_Render_PlainTextOnly_RendersChunkedSegments`,
`DefaultModelCapabilityProfile_Render_ChunkEndingInEllipsis_SetsLongerPostSilenceThanOrdinarySentenceEnd`,
`DefaultModelCapabilityProfile_Render_ChunkEndingInSpacedEllipsis_AlsoSetsEllipsisPostSilence`,
`DefaultModelCapabilityProfile_Render_ChunkEndingInEllipsis_PauseDiffersFromShortAndLongTagPauses`

The ellipsis pause is distinct from both tag-pause durations.

##### Null Argument Rejection

**Stimulus**: `Render` is called with a null span list, then with a null model.

**Expected**: `ArgumentNullException` in both cases.

**Test**: `DefaultModelCapabilityProfile_Render_NullArguments_ThrowsArgumentNullException`
