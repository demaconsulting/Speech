## PathHelpers

**Purpose**: Combine a base path with a caller-supplied path component while guaranteeing the
result stays inside the base directory, so a component such as `../outside.txt` or an absolute path
cannot redirect a file operation outside the directory it is meant to use.

**Data Model**: Stateless static class. It performs no file-system I/O; only string-level path
normalization is applied, so it does not resolve symbolic links.

**Key Methods**: `SafePathCombine(basePath, relativePath)`:

1. Reject a rooted `relativePath` (`Path.IsPathRooted`) with `ArgumentException`, before any
   combination. This applies even when the rooted path lies inside the base directory: a component
   is a relative one by contract, so an absolute path is never accepted.
2. Combine with `Path.Combine`.
3. Resolve both the base and the combined path to fully qualified form with `Path.GetFullPath`.
4. Compute `Path.GetRelativePath(absoluteBase, absoluteCombined)` and reject the combination when that
   relative path is `..`, starts with `..` followed by either directory separator, or is itself
   rooted (the result for a different drive or volume).
5. Return the already validated absolute combined path, never the unnormalized combination, so a
   relative base cannot later resolve against a different working directory than the one the check
   used.

A component that resolves to the base itself (empty, or `.`) is accepted and yields the base. A
component whose name merely starts with two dots (for example `..data`) is not an escape and is
accepted. Case sensitivity of the comparison follows the platform because `GetRelativePath` does.

**Error Handling**:

| Exception | Condition |
| --- | --- |
| `ArgumentNullException` | `basePath` or `relativePath` is null |
| `ArgumentException` | `relativePath` is rooted, escapes the base, or is invalid for the runtime (see below) |
| `NotSupportedException` | A path has an unsupported format |
| `PathTooLongException` | The combined or resolved path exceeds the system limit |

The escape message is `Invalid path component: {relativePath}` with parameter name `relativePath`, so
the displayed text ends with `(Parameter 'relativePath')`.

The CLI calls it only with fixed or generated component names (never raw command-line values), so
the escape message does not reach an operator in practice. The method is thread-safe.

**Dependencies**: `System.IO.Path` only.

**Callers**: `Validation` (its temporary directory, the version and help self-test log files, and the
model-store marker file) and `DoctorCommand` (its model-store write probe file).
