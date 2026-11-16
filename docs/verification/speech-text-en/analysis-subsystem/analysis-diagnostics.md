### AnalysisDiagnostics Verification

#### Verification Approach

`AnalysisDiagnostics` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/AnalysisDiagnosticsTests.cs` against a recording
sink that can be told to throw.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: each test uses its own sink

#### Acceptance Criteria

One request produces exactly one Info event in the `TextEn.Analysis` category with the documented message;
the message matches a template of fixed words and numbers for any counts; a missing sink is accepted and
reports nothing; and a sink that throws is contained, with the event still delivered to it.

#### Test Scenarios

##### One counts event

**Test**: `AnalysisDiagnostics_ReportRequest_Sink_ReceivesOneInfoEventWithCounts`

##### Message contains only fixed words and numbers

**Test**: `AnalysisDiagnostics_FormatMessage_AnyCounts_ContainsOnlyFixedWordsAndNumbers`

##### No sink

**Test**: `AnalysisDiagnostics_ReportRequest_NullSink_DoesNothing`

##### Failing sink

**Test**: `AnalysisDiagnostics_ReportRequest_ThrowingSink_IsContained`
