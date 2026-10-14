### CliModelCatalogFactory Verification

#### Verification Approach

The factory is verified by creating adapters from a `Context` and observing the effect of the global
options on a real catalog over an isolated temporary directory, with no network access.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- A unique temporary store root per test; mirrors point at an unused loopback port

#### Acceptance Criteria

The factory is verified when `--models-dir` and `--voices-dir` are honored, a configured mirror is
applied to model downloads and to voice-pack installs, and a `null` context is rejected.

#### Test Scenarios

##### CliModelCatalogFactory_Create_WithMirrorUrl_AppliesMirrorToDownloads

**Scenario**: The context carries `--mirror-url` pointing at a loopback port with nothing listening, and
a download of a real known model id is attempted.

**Expected**: The download fails with a message that names that loopback host and port, proving the
mirror was applied rather than merely parsed.

##### CliModelCatalogFactory_Create_WithMirrorUrl_AppliesMirrorToVoicePackInstall

**Scenario**: The same dead loopback mirror is configured and a voice-pack install is attempted.

**Expected**: The failure comes from reaching that mirror, proving the pack download uses the same
downloader options.

##### CliModelCatalogFactory_Create_WithModelsDir_UsesGivenRoot

**Scenario**: The context carries `--models-dir` naming a temporary root.

**Expected**: The adapter's `UserVoicesDirectory` lies under that root and the catalog is non-empty. The
test does not assert the model store root itself, so the `--models-dir` evidence is partial; it is
linked to `SpeechCli-ModelCommands-LibraryDelegation` at subsystem level rather than claimed by a unit
requirement.

##### CliModelCatalogFactory_Create_WithVoicesDir_EnumeratesThatFolder

**Scenario**: The context carries `--voices-dir`.

**Expected**: The adapter enumerates voices from that folder (see the adapter verification document).

##### CliModelCatalogFactory_Create_NullContext_ThrowsArgumentNullException

**Scenario**: `Create` is called with a `null` context.

**Expected**: An `ArgumentNullException` is thrown.
