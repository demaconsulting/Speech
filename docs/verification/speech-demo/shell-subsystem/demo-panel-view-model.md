### DemoPanelViewModel Verification

#### Verification Approach

`DemoPanelViewModel` is verified through deterministic unit tests that build navigation entries
over a minimal stub panel view model. The unit touches neither the library nor Avalonia, so every
behavior is verifiable with no audio hardware, no installed model, and no running UI toolkit.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test constructs its own entry; no shared state exists between tests
- **Test doubles**: A minimal `ObservableObject` stub standing in for a panel view model

#### Acceptance Criteria

The unit passes when it carries the title and content it was built with unchanged, and rejects a
missing title, a blank title, and a missing content.

#### Test Scenarios

##### DemoPanelViewModel_Constructor_ValidArguments_CarriesTitleAndContent

**Scenario**: A navigation entry is built from a title and a panel view model.

**Expected**: Both are carried unchanged.


##### DemoPanelViewModel_Constructor_BlankTitle_ThrowsArgumentException

**Scenario**: A navigation entry is built with an empty or whitespace title.

**Expected**: `ArgumentException`, because an untitled navigation entry is unusable.

##### DemoPanelViewModel_Constructor_NullTitle_ThrowsArgumentNullException

**Scenario**: A navigation entry is built with no title.

**Expected**: `ArgumentNullException`.


##### DemoPanelViewModel_Constructor_NullContent_ThrowsArgumentNullException

**Scenario**: A navigation entry is built with no content.

**Expected**: `ArgumentNullException`.

