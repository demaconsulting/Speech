# Round 10 Partition S Handoff

## docs/reqstream/speech.yaml (Partition F)

Existing parent links to keep: `ClosedVocabularyAliasCanonicalization`, `MockableContract`,
`MockableSynthesisStreaming`, `EngineDisposalOrdering`, `FaultContainment`, `DeterministicCancellation`,
`Layer2RenderingPause`, `UnavailableFallbackOperationsThrow` (all first IDs stayed stable).

New subsystem-level IDs (`Speech-Synthesis-` prefix) needing a parent link in `speech.yaml`:

- `ClosedVocabularyAliasUniqueness` (sibling of `ClosedVocabularyAliasCanonicalization`)
- `Layer2RenderingEllipsisPause` (sibling of `Layer2RenderingPause`)
- `Layer2RenderingPlanValidity`
- `MockableSynthesisPlayback`, `MockableSynthesisSessionReuse` (siblings of `MockableSynthesisStreaming`)
- `EngineBackendDisposedOnce` (sibling of `EngineDisposalOrdering`)
- `FaultContainmentPlaybackDevice` (sibling of `FaultContainment`)
- `CancellationStopsOperation`, `CancellationCallerTokenScope` (siblings of `DeterministicCancellation`)
- `UnavailableSessionOperationsThrow` (sibling of `UnavailableFallbackOperationsThrow`)

Test moved: the NoAliasIsShared test now links to `ClosedVocabularyAliasUniqueness`.

## Other shared files

- `.reviewmark.yaml`: no new test or source files, no change needed.
- No test files were added; new tests live in `DefaultModelCapabilityProfileTests.cs` and `SynthesisSessionTests.cs`.
- `SpeechPlan`/`SpeechSegment` now validate input; `ChatterboxTurboCapabilityProfile` tests (other partition's
  project) should be rerun.
- SynthesisSubsystem net9.0 failure was not reproducible (4 loops, 987 tests green); guards raised 5 s to 30 s.
