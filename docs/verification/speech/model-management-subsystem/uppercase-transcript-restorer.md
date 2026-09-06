### UppercaseTranscriptRestorer Verification

#### Verification Approach

`UppercaseTranscriptRestorer` is verified through deterministic, purely in-memory unit tests -
no network, no audio, no real recognizer involved. Theory-driven tests cover every entry in the
28-word contraction table and every one of the 12 deliberately-ambiguous exclusions
individually, plus capitalization, terminal-punctuation, idempotency, null/empty-input, and the
cheap provisional path's deliberate omissions. The one-time, real (non-mocked) audio spike that
proves this restorer against genuine recognizer output (real LibriTTS-synthesized audio through
the real `SherpaOnnxRecognitionEngine`) was run separately and its evidence is recorded in
`SherpaOnnxZipformerEnRecognitionModel`'s XML remarks and this phase's completion report; it is
not part of the repeatable automated test suite.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Fully in-memory; no filesystem, network, or audio device access

#### Acceptance Criteria

Every contraction table entry restores its apostrophe when it appears as a whole word in
lowercased input; every deliberately-ambiguous form is left unchanged; the standalone word "I"
and the first alphabetic character are capitalized; text that already ends in `.`/`?`/`!` keeps
its existing terminator, otherwise a period is appended; empty or whitespace-only input returns
the empty string without throwing; a null input throws `ArgumentNullException`; re-running
`RestoreFinal` on its own output is idempotent; `RestoreProvisional` capitalizes "I" and the
first letter only, deliberately leaving contractions unrestored and never appending terminal
punctuation.

#### Test Scenarios

##### Every contraction table entry restores its apostrophe

**Test**: `RestoreFinal_ContractionForm_RestoresApostrophe`

##### Every deliberately-ambiguous form is left unchanged

**Test**: `RestoreFinal_DeliberatelyAmbiguousForm_IsLeftAlone`

##### The standalone word "I" is capitalized

**Test**: `RestoreFinal_StandaloneI_IsCapitalized`

##### The first letter is capitalized

**Test**: `RestoreFinal_FirstLetter_IsCapitalized`

##### Text already ending in terminal punctuation keeps its existing terminator

**Test**: `RestoreFinal_AlreadyTerminated_KeepsExistingTerminator`

##### Text with no terminator has a period appended

**Test**: `RestoreFinal_NoTerminator_AppendsPeriod`

##### Empty or whitespace-only input returns the empty string

**Test**: `RestoreFinal_EmptyOrWhitespace_ReturnsEmptyString`

##### Null input throws ArgumentNullException

**Test**: `RestoreFinal_NullInput_ThrowsArgumentNullException`

##### Already-restored text is idempotent under RestoreFinal

**Test**: `RestoreFinal_AlreadyRestoredText_IsIdempotent`

##### RestoreProvisional does not restore contraction apostrophes

**Test**: `RestoreProvisional_ContractionForm_DoesNotRestoreApostrophe`

##### RestoreProvisional does not append a terminator

**Test**: `RestoreProvisional_NoTerminator_DoesNotAppendPeriod`

##### RestoreProvisional capitalizes the standalone "I" and the first letter

**Test**: `RestoreProvisional_StandaloneIAndFirstLetter_AreCapitalized`

##### RestoreProvisional returns the empty string for empty or whitespace-only input

**Test**: `RestoreProvisional_EmptyOrWhitespace_ReturnsEmptyString`

##### RestoreProvisional throws ArgumentNullException for null input

**Test**: `RestoreProvisional_NullInput_ThrowsArgumentNullException`
