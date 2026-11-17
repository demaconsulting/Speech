### TrainingRecord Verification

#### Verification Approach

`TrainingRecord` (with `TrainingSettings`, `EvaluationSettings`, `TrainingInputs` and `TrainingResult`) is verified by
automated unit tests in `test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/TrainingRecordTests.cs`.
The tests build a record with a distinct value in every field, write it to text and read it back, and damage the text in
one place at a time (a removed property, a setting out of range). The culture test sets the current culture to `de-DE`
and restores it in a `finally` block. Nothing is mocked.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: the unit is a pure function of the record; the culture test restores the culture

#### Acceptance Criteria

A record parsed from its own text has equal training settings, evaluation settings (including the seeds), inputs and
result, including the temperature; the text of the same record is equal on every rendering, has no carriage return
and ends with a closing brace and a line feed; under `de-DE` the text is the same and the temperature reads back
unchanged; an empty text, `not json`, `[]`, `{}` and a record of schema 2 throw `InvalidDataException`; a record without
the `imageSha256` property throws with a message that names it; a hash size of 99, a natural-row weight of 0, a
confidence of 5 percent and 1 resample each throw `InvalidDataException`; the default settings are valid and carry a
gate of 930 per mille, a confidence of 95 percent, 10,000 resamples and the default training seed among the evaluation
seeds.

#### Test Scenarios

##### Round trip

**Test**: `TrainingRecord_ToJsonThenParse_RoundTripsEveryField`

Every field of the sample record survives writing and reading.

##### Deterministic text

**Tests**: `TrainingRecord_ToJson_IsDeterministicWithLineFeedsAndFinalNewline`,
`TrainingRecord_ToJson_UnderAnotherCulture_IsUnchanged`

Two renderings are equal and have line feeds only and a final newline; a rendering under another culture is equal, and
its temperature parses back.

##### Validation

**Tests**: `TrainingRecord_Parse_MalformedText_ThrowsInvalidDataException` (a theory over five texts),
`TrainingRecord_Parse_MissingProperty_ThrowsNamingTheProperty`,
`TrainingRecord_Parse_SettingOutOfRange_ThrowsInvalidDataException` (a theory over four settings)

Each damaged text throws `InvalidDataException`; the message for a missing property names the property.

##### Default gate

**Test**: `TrainingSettings_Default_AreValidAndCarryTheGate`

The default training and evaluation settings pass their validation and carry the thresholds listed above.
