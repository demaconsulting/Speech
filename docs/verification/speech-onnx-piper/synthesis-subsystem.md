## SynthesisSubsystem Verification

### Verification Approach

The SpeechOnnxPiper SynthesisSubsystem is verified by deterministic tests in
`test/DemaConsulting.Speech.Onnx.Piper.Tests`. The units are verified in isolation by their own tests (see their
verification documents): the phoneme pipeline against the golden corpora with no graph, the engine against a stand-in
graph. The subsystem tests in `SynthesisSubsystem/SynthesisSubsystemTests.cs` run the two together as the model builds
them: a backend created from an installed stand-in voice (a tiny ONNX graph with the four inputs of the Piper graph
whose output echoes the ids, the three scales, the speaker and the input length it was fed), the real English text
analysis of the model, the real phonemizer and renderer, and the real symbol table of the voice (a trimmed copy of its
configuration). What reaches the graph is therefore observed exactly, and the reading that the text analysis chose can
be seen in the ids.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the library
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Onnx.Piper.Tests`
- **Isolation**: each test class instance writes its own installed stand-in voice to a scratch directory; no network, no
  real voice

### Acceptance Criteria

The subsystem tests pass when: for `They plan to record the song.` the ids with the annotations equal the ids without
them (the verb is the default reading) and for `Here is the record.` they differ (the noun); `Welcome Zorblax` feeds at
least ten ids more than `Welcome`; with the symbol `ɑ` removed from the table `hot` is still spoken and the engine
counts exactly one skipped symbol and lists `ɑ`; a speed of 1 gives the length scale 1.0, a speed of 2 gives 0.5, and
the speakers 5, 903 and 5000 reach the graph as 5, 903 and 903; forty sentences give an audio whose length is the sum
over their utterances of the framed ids plus the five echoed values, with at least three utterances; an empty text, a
blank text and an emoji give empty audio at 22,050 Hz; the probe succeeds on a runnable session; and a backend can be
disposed twice and refuses synthesis afterwards.

### Test Scenarios

#### Reading-aware phonemes

**Tests**: `SynthesisSubsystem_Synthesize_HeteronymSentence_SpeaksTheChosenReading`,
`SynthesisSubsystem_Synthesize_UnknownWord_IsStillSpoken`

The verb and noun sentences are run through the backend with and without the annotations of the model's analysis; the
unknown word `Zorblax` is run after `Welcome` alone and together with it.

#### Unknown symbols

**Test**: `SynthesisSubsystem_Synthesize_SymbolOutsideTheTable_IsCountedAndReportedWhileTheRestIsSpoken`

An engine built over a configuration without the symbol `ɑ` speaks `Hello there.` with no loss and then `hot`, which
loses exactly one symbol that the engine reports.

#### Graph inputs

**Test**: `SynthesisSubsystem_Synthesize_SpeedAndSpeaker_ReachTheGraph`

The echoed length scale and speaker are compared for speeds 1 and 2 and speakers 5, 903 and 5000.

#### Text splitting

**Tests**: `SynthesisSubsystem_Synthesize_LongText_IsSpokenWholeAndJoined`,
`SynthesisSubsystem_Synthesize_SoundlessText_ReturnsEmptyAudio`

The audio of forty sentences has exactly the length that the pipeline's utterances predict; the empty, blank and emoji
texts give empty audio.

#### Session lifecycle

**Tests**: `SynthesisSubsystem_Probe_RunnableSession_DoesNotThrow`,
`SynthesisSubsystem_Backend_Dispose_IsIdempotentAndEndsUse`

The probe runs the real pipeline's probe sentence through the stand-in graph without error; disposing a backend twice
does not throw, and a synthesis after disposal throws `ObjectDisposedException`.

See each unit's own verification document for its detailed scenarios: `onnx-piper-synthesis-engine.md` and
`piper-phoneme-pipeline.md`; see also *SpeechOnnxPiper System Verification*.
