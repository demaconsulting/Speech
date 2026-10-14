## DemaConsulting.TestResults

### Purpose

`DemaConsulting.TestResults` is used by the SpeechCli tool's built-in self-validation
(`speech-cli --validate`) to build a collection of test results and serialize it as a Visual
Studio TRX file or a JUnit XML file. It was chosen because it is a small, maintained package from
the same organization that already provides both serializers, so the tool does not hand-roll two
result file formats.

### Features Used

- `DemaConsulting.TestResults.TestResults` and `TestResult` to hold one result per self-validation
  check (name, class name, code base, outcome, duration, error message)
- `DemaConsulting.TestResults.IO.TrxSerializer.Serialize` to produce TRX content for a `.trx`
  results path
- `DemaConsulting.TestResults.IO.JUnitSerializer.Serialize` to produce JUnit XML content for a
  `.xml` results path

### Integration Pattern

SpeechCli references the package directly from `DemaConsulting.Speech.Cli.csproj`, which is the
single source of truth for the pinned version. The internal `Validation` unit is the only consumer:
it creates the result objects, adds one per check, and, when `--results <file>` is supplied, selects
the serializer from the file extension and writes the returned text to the file. Neither the Speech
library nor any other SpeechCli unit references the package, and no package type appears in any
public API.
