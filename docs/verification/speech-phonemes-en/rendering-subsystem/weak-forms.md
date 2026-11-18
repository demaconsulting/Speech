### WeakForms Verification

#### Verification Approach

`WeakForms` is verified by unit tests in `test/DemaConsulting.Speech.Phonemes.En.Tests/RenderingSubsystemTests.cs` (the
`WeakForms_*` tests) that call `TryGet` directly, or through the renderers for the espeak-style details, with the real
phonemizer producing the items. That every form of the table is a symbol string of the Piper voice is the subsystem
coverage test; the effect of the table on a sentence is the subsystem and system weak-form tests.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: no files or network; the table is immutable

#### Acceptance Criteria

`The` is found with the reduced form `ðə` before a consonant and a different form before a vowel, and is not demoted;
`but` is found as demoted with no form; `cat` is not found. `it is at all` renders with the flapped `ɪɾ` and `æɾ` for
Piper and plain `ɪt` and `æt` for Kokoro with weak forms; `on the table for the road` renders with an unmarked `on` and
the group `fɚðə` for Piper; `made between` has the secondary stress for Piper and the primary stress for Kokoro.

#### Test Scenarios

##### Form by following sound

**Test**: `WeakForms_TryGet_ListedWords_GiveTheirFormByFollowingSound`

##### Espeak-style details

**Test**: `WeakForms_TryGet_EspeakStyleDetails_AreForPiperOnly`
