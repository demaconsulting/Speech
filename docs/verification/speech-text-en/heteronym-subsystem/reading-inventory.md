### ReadingInventory Verification

#### Verification Approach

`ReadingInventory` and the reading id grammar (`ReadingIdSyntax`) are verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/HeteronymSubsystem/ReadingInventoryTests.cs`. The embedded inventory is real:
the tests read it through `ReadingInventory.Default` and the assembly's manifest resources, and check the facts a data
change must not alter silently (the revision and the counts). The parser is verified through the internal `Parse`
factory with hand-written tables that each hold one defect, so every rejection is attributed to one rule. No fake is
needed.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: no test needs network access or files; the culture test restores the current culture

#### Acceptance Criteria

The embedded tables load to the pinned revision and counts and are assembly resources under the fixed logical names (and
no model resource exists); every reading id of the embedded inventory satisfies the grammar and the sorted list is
ordinal and unique; the candidates of a form are in the order of the table rows and the lookup folds ASCII case only
(unknown words, the empty text, compounds, spaces, a period, non-ASCII letters and the Kelvin sign have no candidates)
under any culture; a null argument to a public lookup throws `ArgumentNullException`; `Contains` compares ordinally; the
whole-token lookup agrees with the public lookup through the internal accessors; the public collections are
read-only; hand-built valid tables parse (with the supplied revision) and a deprecated reading is inactive; every defect
of either table (a byte-order mark, a carriage return, a wrong header, an empty table, a wrong column count, an
ill-formed or duplicate id, an empty lemma, an unknown status, an unknown reading, a bad form, an empty kind, a
duplicate pair) is rejected with an `InvalidDataException` that names the table, the line and a fixed reason and never
contains the data; 16 candidates are accepted and 17 rejected; an over-long line and too many rows are rejected; and the
id grammar accepts exactly two or three well-formed segments up to 64 characters.

#### Test Scenarios

##### Embedded tables

**Tests**: `ReadingInventory_Default_EmbeddedTables_MatchPinnedVersionAndCounts`,
`ReadingInventory_Default_EmbeddedTables_AreAssemblyResourcesWithFixedNames`,
`ReadingInventory_ReadingIds_AllSatisfyGrammarAndAreOrdinalSorted`

The default inventory has revision 2, 395 readings, 501 distinct forms of which 350 have more than one candidate, and is
the same instance on every access, so a change of the data cannot pass without a deliberate decision about the
revision. The manifest holds the resources `DemaConsulting.Speech.Text.En.Resources.readings.tsv` and
`DemaConsulting.Speech.Text.En.Resources.forms.tsv` and no `heteronyms.bin`. Every id satisfies the grammar and is
contained, the list equals its ordinal sort and has no duplicate.

##### Candidate lookup

**Tests**: `ReadingInventory_CandidatesFor_AsciiFold_ReturnsRowOrder` (a theory over `read` in three cases and
`record`), `ReadingInventory_CandidatesFor_NotAForm_ReturnsEmpty` (a theory over the empty text, an unknown word,
`wind-up`, a leading and a trailing space, a word with an accented letter, the Kelvin sign, a digit inside a word and a
trailing period), `ReadingInventory_PublicLookups_NullArgument_ThrowsArgumentNullException`,
`ReadingInventory_Contains_ExactId_ComparesOrdinally`,
`ReadingInventory_CandidatesFor_TurkishCulture_FoldsAsciiOnly`

`read` in any case gives `read.past` then `read.present` and `record` gives `record.noun` then `record.verb`, the order
of the rows of the forms table. Anything that is not exactly a form has no candidates. A null argument throws.
`Contains` accepts the exact id and rejects another case and an unknown id. Under `tr-TR`, `RECORD` is found and a word
with the dotted capital I (at the end of `REC` or inside `m` and `nd`) is not.

##### Internal accessors and immutability

**Tests**: `ReadingInventory_InternalAccessors_AgreeWithPublicLookup`,
`ReadingInventory_ReadingIdsAndCandidates_AreReadOnly`

For `record` the candidate indexes name `record.noun` and `record.verb`, are
active and carry the label salt of the id and the role salt of the role, and the reverse index lookup agrees. The
public id and candidate collections are read-only through a cast and throw `NotSupportedException` when written.

##### Parser

**Tests**: `ReadingInventory_Parse_ValidTables_BuildsInventory`, `ReadingInventory_Parse_DeprecatedStatus_IsInactive`,
`ReadingInventory_Parse_NullReader_ThrowsArgumentNullException`,
`ReadingInventory_Parse_MalformedReadings_ThrowsInvalidDataExceptionWithLineAndReason` (a theory over twelve defects of
the readings table),
`ReadingInventory_Parse_MalformedForms_ThrowsInvalidDataExceptionWithLineAndReason` (a theory over ten defects of the
forms table), `ReadingInventory_Parse_Malformed_MessageNeverContainsTheData`

A table of two readings and three form rows builds an inventory with the supplied revision, one ambiguous and one
single-candidate form; a `deprecated` status is parsed and inactive. Null readers throw. Each defect row is rejected
with a message that holds the table name, the one-based line and the fixed reason (for example `readings.tsv line 3:
duplicate reading id`), and a sentinel form that violates the form rule does not appear in the message.

##### Parser limits

**Tests**: `ReadingInventory_Parse_SeventeenCandidates_IsRejected`,
`ReadingInventory_Parse_SixteenCandidates_IsAccepted`, `ReadingInventory_Parse_OverLongLine_IsRejected`,
`ReadingInventory_Parse_TooManyRows_IsRejected`

Seventeen candidate readings of one form are rejected at the seventeenth row (`forms.tsv line 18`) and sixteen are
accepted. A line with a 1,100-character description is rejected as too long on its line. A table of 5,001 readings and a
table of 5,001 forms are each rejected as having too many rows.

##### Reading id grammar

**Tests**: `ReadingIdSyntax_IsValid_Grammar_AcceptsOnlyWellFormedIds` (a theory over twenty-two ids),
`ReadingIdSyntax_IsValid_LengthLimit_Is64`, `ReadingIdSyntax_RoleOf_ReturnsTextAfterFirstDot`

The theory accepts `record.noun`, `abstract.adj-noun`, `a.b.c` and `x1.y2-z3` and rejects the empty text, one segment,
four segments, upper case, a space, a trailing space, an empty segment, an edge hyphen on either side of a dot, an
underscore, a pipe, a colon and a Cyrillic letter. An id of exactly 64 characters is valid and one of 65 is not. The
role of `record.noun` is `noun` and of `a.b.c` is `b.c`.
