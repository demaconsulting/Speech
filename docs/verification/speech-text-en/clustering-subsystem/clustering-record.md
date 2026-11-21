### ClusteringRecord Verification

#### Verification Approach

`ClusteringRecord` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/ClusteringSubsystem/ClusteringRecordTests.cs`. The tests use a record
with a distinct value in every field, check the written text and parse written and edited documents. Nothing is mocked.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Not used**: files and the network

#### Acceptance Criteria

**Format.** The text starts with the schema and the settings in the fixed order with two-space indentation, contains the
inputs and the tokenizer version, ends with the result and a final LF, has no CR, and parses back to an equal record
that writes the same bytes.

**Parse.** Invalid JSON, another schema, a renamed field and a field of the wrong type throw `InvalidDataException`.

#### Test Scenarios

##### Format

**Tests**: `ClusteringRecord_Write_FixedTextForm`, `ClusteringRecord_Parse_WrittenRecord_RoundTrips`

The text begins `{\n  "schema": 1,\n  "settings": {\n    "clusters": 512,` and ends
`"tokenCoveragePerTenThousand": 9712\n  }\n}\n`; the parsed record equals the original and writes identical bytes.

##### Parse

**Tests**: `ClusteringRecord_Parse_InvalidDocuments_Throw`

`{ not json`, schema 2, `tableHash` in place of `tableSha256` and a quoted `usedBooks` each throw.
