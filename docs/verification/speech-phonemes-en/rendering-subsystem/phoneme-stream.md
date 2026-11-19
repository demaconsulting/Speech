### PhonemeStream Verification

#### Verification Approach

`PhonemeStream` is verified by unit tests in `test/DemaConsulting.Speech.Phonemes.En.Tests/RenderingSubsystemTests.cs`
(the `PhonemeStream_*` tests) that call `Join` and `Split` directly with items from the real phonemizer and pieces
written in the test (the item text as its own rendering), so the layout and the cut are checked without the sound rules
of either voice. The effect on both renderers is the subsystem parts test.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: no files or network; the helpers are stateless

#### Acceptance Criteria

The items of `Hi, (there) 😀` with the pieces `hˈaɪ`, `,`, `(`, `ðɛɹ`, `)` and an empty piece are joined as
`hˈaɪ, (ðɛɹ)` when `(` is an opening mark. Twelve repetitions of `Go now, please. ` split with a limit of 40 give more
than three parts, each at most 40 symbols, each but the last ending at a comma or a period, and the parts together
contain every word of the whole; an empty sequence gives no part.

#### Test Scenarios

##### Layout

**Test**: `PhonemeStream_Join_WordsAndMarks_AreSpacedByTheSharedLayout`

##### Splitting

**Tests**: `PhonemeStream_Split_ItemsBeyondTheLimit_AreCutAtClauseMarksWithoutLoss`,
`PhonemeStream_Split_TextWithoutMarks_IsCutBeforeAConjunctionNeverAfterAFunctionWord`

A text of 160 characters with no mark at all is cut before the last conjunction that fits
(`...bright blue boats | but he sailed...`), and with a smaller limit no part ends on `of`, `the`, `and`, `but`, `in` or
`its`, whatever the budget; the parts together are the whole text.
