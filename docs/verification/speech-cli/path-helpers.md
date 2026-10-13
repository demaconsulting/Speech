## PathHelpers Verification

### Verification Approach

`PathHelpers` is verified with unit tests that call `SafePathCombine` with relative and absolute
bases and with escaping, rooted and null components, asserting the returned path or the exception
thrown. The method does no file-system I/O, so no test creates a file or directory.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Stateless; each test is independent and touches no shared state
- **Test doubles**: None

### Acceptance Criteria

The unit passes when a safe component yields the fully qualified path that matches what the
containment check validated (for relative and absolute bases), an escaping or rooted component is
rejected with `ArgumentException`, and null arguments are rejected with `ArgumentNullException`.

### Test Scenarios

#### PathHelpers_SafePathCombine_RelativeBasePath_ReturnsAbsolutePath

**Scenario**: A relative base `relative-base` is combined with `child.txt`.

**Expected**: The result equals the fully qualified form of the combination and is rooted.

#### PathHelpers_SafePathCombine_AbsoluteBasePath_ReturnsAbsolutePath

**Scenario**: An absolute temporary-folder base is combined with `child.txt`.

**Expected**: The result equals the fully qualified form of the combination.

#### PathHelpers_SafePathCombine_EscapingRelativePath_ThrowsArgumentException

**Scenario**: `../outside.txt` is combined with a relative base.

**Expected**: `ArgumentException`.

#### PathHelpers_SafePathCombine_RootedRelativePath_ThrowsArgumentException

**Scenario**: A rooted path outside the base is supplied as the component.

**Expected**: `ArgumentException`, because a rooted component is never accepted.

#### PathHelpers_SafePathCombine_RootedPathInsideBase_ThrowsArgumentException

**Scenario**: A rooted path that lies inside the base directory is supplied as the component
(regression for a defect where it was accepted).

**Expected**: `ArgumentException` with parameter name `relativePath`.

#### PathHelpers_SafePathCombine_NullArguments_ThrowArgumentNullException

**Scenario**: A null base, then a null component.

**Expected**: `ArgumentNullException` for each.

### Not Covered By A Dedicated Test

- A component that resolves to the base itself, and a name that merely begins with two dots, are
  accepted by design; no test asserts either.
- The `NotSupportedException` and `PathTooLongException` conditions come from the runtime's path
  functions and are not exercised.
