### DownloadProgressReporter Verification

#### Verification Approach

The reporter is verified by feeding progress reports to a reporter created over a `Context` that
captures its output, and reading the printed lines.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- A `Context` writing to a captured stream; no network

#### Acceptance Criteria

The reporter is verified when a known total prints one line per whole percent without repeats, an
unknown total prints kilobytes, and `null` arguments are rejected.

#### Test Scenarios

##### DownloadProgressReporter_Create_KnownTotal_PrintsOneLinePerWholePercent

**Scenario**: Several reports with the same whole percent and then increasing percents are raised.

**Expected**: One line per whole percent, formatted `{label}: {percent}%`, with no repeats.

##### DownloadProgressReporter_Create_UnknownTotal_ReportsKilobytes

**Scenario**: Reports are raised with no known total.

**Expected**: Lines of the form `{label}: {n} KB` are printed, one per whole kilobyte.

##### DownloadProgressReporter_Create_NullArguments_ThrowArgumentNullException

**Scenario**: `Create` is called with a `null` context or a `null` describe delegate.

**Expected**: An `ArgumentNullException` is thrown. This is a null guard and carries no requirement.
