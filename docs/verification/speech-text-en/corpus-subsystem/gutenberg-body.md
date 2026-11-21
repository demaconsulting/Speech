### GutenbergBody Verification

#### Verification Approach

`GutenbergBody` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/CorpusSubsystem/GutenbergBodyTests.cs`. The unit is a pure
function of bytes, so the tests build downloads in code (a header, the markers, a body and a footer, with CRLF line
endings and optionally a byte order mark) and compare the extracted text and counts. Nothing is mocked.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Not used**: files, the network and any real book

#### Acceptance Criteria

**Body.** Only the text between the markers remains, with LF endings and one final LF; credit paragraphs near the start
and paragraphs that name the archive are dropped and counted, a credit-like paragraph after the first 40 body lines is
kept, underscores and trailing blanks are removed and blank runs collapse; the same download gives the same pin.

**Reject.** Invalid UTF-8, a missing start or end marker and an empty body throw `InvalidDataException`.

#### Test Scenarios

##### Body

**Tests**: `GutenbergBody_Extract_HeaderAndFooter_KeepsOnlyTheBody`,
`GutenbergBody_Extract_CreditsAndArchiveNames_AreDroppedAndCounted`, `GutenbergBody_Extract_CreditBeyondWindow_IsKept`,
`GutenbergBody_Extract_UnderscoresAndTrailingBlanks_AreRemoved`, `GutenbergBody_Extract_SameDownload_SamePin`

A download with a byte order mark, CRLF endings, leading and repeated blank lines and trailing spaces gives exactly
`First line of the book.\nSecond line.\n\nNext paragraph.\n` with matching bytes and SHA-256. A "Produced by" paragraph
and a paragraph naming the archive across two lines are dropped and counted once each. A "Produced by" paragraph after
41 lines is kept. `It was _very_ minute.` loses its underscores and a line holding only an underscore becomes a blank
line. A lowercase "Start of this ... E-Book" marker and an end marker without a space are accepted, and two extractions
give the same 64-digit pin.

##### Reject

**Tests**: `GutenbergBody_Extract_InvalidDownloads_Throw`

A download without the start marker, one without the end marker, one whose only paragraph names the archive and one
ending in a truncated UTF-8 sequence each throw `InvalidDataException`.
