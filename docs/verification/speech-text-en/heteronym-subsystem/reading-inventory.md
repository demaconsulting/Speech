### ReadingInventory Verification

#### Verification Approach

`ReadingInventory` and the reading id grammar (`ReadingIdSyntax`) are verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/HeteronymSubsystem/ReadingInventoryTests.cs`. The embedded inventory is real:
the tests read it through `ReadingInventory.Default` and check the facts a data change must not alter silently (the
revision and that no reading or form was lost). The parser is verified through the internal `Parse` factory with
hand-written tables that each hold one defect, so every rejection is attributed to one rule. No fake is needed.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: no test needs network access or files; the culture test restores the current culture

#### Acceptance Criteria

- **Id grammar**: the grammar accepts exactly two or three well-formed segments up to 64 characters; every reading id
  of the embedded inventory satisfies it and the list is ordinal and unique
- **Strict tables**: the inventory shipped with the assembly has the current revision, at least the released numbers of
  readings and forms and the candidate readings of well-known heteronyms; hand-built valid tables parse and a
  deprecated reading is inactive; every defect of either table is rejected with an `InvalidDataException` that names
  the table, the line and a fixed reason and never contains the data; 16 candidates are accepted and 17 rejected; an
  over-long line and too many rows are rejected
- **Lookup**: the candidates of a form are in the order of the table rows and the lookup folds ASCII case only under
  any culture (unknown words, the empty text, compounds, spaces and non-ASCII letters have no candidates); `Contains`
  compares ordinally; a null argument throws `ArgumentNullException`; the public collections are read-only

#### Test Scenarios

##### Reading id grammar

**Tests**: `ReadingIdSyntax_IsValid_Grammar_AcceptsOnlyWellFormedIds`, `ReadingIdSyntax_IsValid_LengthLimit_Is64`,
`ReadingInventory_ReadingIds_AllSatisfyGrammarAndAreOrdinalSorted`

The theory accepts `record.noun`, `abstract.adj-noun` and `a.b.c` and rejects one segment, four segments, upper case, an
empty segment, an edge hyphen and a Cyrillic letter. An id of exactly 64 characters is valid and one of 65 is not. Every
id of the embedded inventory satisfies the grammar and is contained, the list equals its ordinal sort and has no
duplicate.

##### Embedded and parsed tables

**Tests**: `ReadingInventory_Default_EmbeddedTables_MatchPinnedVersionAndCounts`,
`ReadingInventory_Default_ShippedWithAssembly_ProvidesCandidateReadingsOfKnownHeteronyms`,
`ReadingInventory_Parse_ValidTables_BuildsInventory`, `ReadingInventory_Parse_DeprecatedStatus_IsInactive`,
`ReadingInventory_Parse_MalformedReadings_ThrowsInvalidDataExceptionWithLineAndReason`,
`ReadingInventory_Parse_MalformedForms_ThrowsInvalidDataExceptionWithLineAndReason`,
`ReadingInventory_Parse_Malformed_MessageNeverContainsTheData`

The default inventory has the pinned revision and at least the readings and forms of that revision, so a change of the
data cannot pass without a deliberate decision about the revision, and it loses nothing silently (a versioned
data-integrity check, not a claim about implementation objects). From the assembly alone, with no file or download,
`record` has the candidates `record.noun` and `record.verb` and `READ` has `read.past` and `read.present`. That the
tables are linked from the data folder under fixed resource names, and that the default is built once, are design
choices recorded in the *ReadingInventory Design*. A table of two readings and three form rows builds an inventory with
the supplied revision, one ambiguous and one single-candidate form; a `deprecated` status is parsed and inactive. Each
defect row (a byte-order mark, a carriage return, a wrong header, an empty table, a wrong column count, an ill-formed or
duplicate id, an unknown status, an unknown reading, a bad form, an empty kind, a duplicate pair) is rejected with a
message that holds the table name, the one-based line and the fixed reason (for example
`readings.tsv line 3: duplicate reading id`), and a sentinel form that violates the form rule does not appear in the
message.

##### Parser limits

**Tests**: `ReadingInventory_Parse_SeventeenCandidates_IsRejected`,
`ReadingInventory_Parse_SixteenCandidates_IsAccepted`, `ReadingInventory_Parse_OverLongLine_IsRejected`,
`ReadingInventory_Parse_TooManyRows_IsRejected`

Seventeen candidate readings of one form are rejected at the seventeenth row and sixteen are accepted. A line with a
1,100-character description is rejected as too long on its line. A table of 5,001 readings and a table of 5,001 forms
are each rejected as having too many rows.

##### Candidate lookup and immutability

**Tests**: `ReadingInventory_CandidatesFor_AsciiFold_ReturnsRowOrder`,
`ReadingInventory_CandidatesFor_NotAForm_ReturnsEmpty`, `ReadingInventory_CandidatesFor_TurkishCulture_FoldsAsciiOnly`,
`ReadingInventory_Contains_ExactId_ComparesOrdinally`,
`ReadingInventory_PublicLookups_NullArgument_ThrowsArgumentNullException`,
`ReadingInventory_ReadingIdsAndCandidates_AreReadOnly`,
`ReadingInventory_IsAdjective_ReadsThePartOfSpeechHint`

`READ` gives `read.past` then `read.present` and `record` gives `record.noun` then `record.verb`, the order of the rows
of the forms table. The empty text, an unknown word, a word with a leading space and the Kelvin sign have no
candidates. Under `tr-TR`, `RECORD` is found and a word with the dotted capital I is not. `Contains` accepts the exact
id and rejects another case and an unknown id. A null argument throws. The public id and candidate collections are
read-only through a cast and throw `NotSupportedException` when written. `minute.tiny` (`ADJ`), `close.adj-noun`
(`ADJ|NOUN`) and `content.satisfied` (`ADJ|NOUN|VERB`) are adjective readings; `minute.time`, `close.verb` and
`record.noun` are not.
