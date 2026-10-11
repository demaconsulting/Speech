### NemotronVocabulary Verification

#### Verification Approach

Verified by deterministic in-memory unit tests plus one file-loading test using a scratch file.

#### Test Environment

xUnit v3 under the .NET SDK, in `test/DemaConsulting.Speech.Onnx.NemotronStt.Tests`
(`NemotronVocabularyTests.cs`).

#### Acceptance Criteria

Language ids are the ordinals among the `<unk>` and locale tokens; unknown locales are reported
as absent; the blank id is the blank token's index or the last index; detokenization maps the
word marker to a space, strips locale markers, collapses whitespace, and trims; an empty
vocabulary is rejected; and a vocabulary file loads one token per line.

#### Test Scenarios

##### Known locales return their ordinal

**Test**: `TryGetLanguageId_KnownLocales_ReturnsOrdinal`

##### Unknown locale returns false

**Test**: `TryGetLanguageId_UnknownLocale_ReturnsFalse`

##### Blank id is the blank token index

**Test**: `BlankId_IsBlankTokenIndex`

##### Blank id defaults to the last index

**Test**: `BlankId_NoBlankToken_IsLastIndex`

##### Detokenize normalizes text

**Test**: `Detokenize_Tokens_NormalizesText`

##### Detokenize of nothing returns an empty string

**Test**: `Detokenize_Empty_ReturnsEmpty`

##### Empty vocabulary is rejected

**Test**: `Constructor_Empty_Throws`

##### Load reads tokens from a file

**Test**: `Load_File_ReadsTokens`
