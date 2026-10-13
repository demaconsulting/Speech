### SpeechModelCatalogAdapter Verification

#### Verification Approach

The adapter is verified against a real `SpeechModelCatalog` and `ReferenceVoiceLibrary` rooted at an
isolated temporary directory. Every model in the compiled-in list starts `NotDownloaded` and no test
triggers a download, so no network access is needed. The engine and format members are verified in the
`ICliModelCatalog` verification document.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- A unique temporary store root and voices folder per test, deleted afterward

#### Acceptance Criteria

The adapter is verified when the compiled-in models (including the voice-cloning model) are listed, the
voice-cloning model and the voice commands see the same voices, a damaged voice file does not break
listing, uninstall and cleanup of a never-installed model or pack are safe no-ops, an unknown model id
is an `ArgumentException`, and a failed composition propagates its exception.

#### Test Scenarios

##### SpeechModelCatalogAdapter_Enumerate_ReturnsCompiledInKnownModels

**Scenario**: `Enumerate` is called on a fresh adapter.

**Expected**: The Sherpa, Kokoro, Nemotron and Chatterbox Turbo models (matched by the Chatterbox base
model id prefix) are present and all are `NotDownloaded`.

##### SpeechModelCatalogAdapter_UninstallAndCleanUpLeftovers_NeverInstalledModel_DoNotThrow

**Scenario**: `UninstallAsync` and `CleanUpLeftoversAsync` are called for a known, never-installed model.

**Expected**: Neither throws.

##### SpeechModelCatalogAdapter_DownloadAsync_UnknownModelId_ThrowsArgumentException

**Scenario**: `DownloadAsync` is called with an id absent from the known-model list.

**Expected**: An `ArgumentException` is thrown.

##### SpeechModelCatalogAdapter_Voices_ListsUserAndVoicesDirVoices

**Scenario**: Audio files exist in the user voices folder and in the explicit voices folder.

**Expected**: `EnumerateVoices` lists voices from both.

##### CliModelCatalogFactory_Create_WithVoicesDir_EnumeratesThatFolder

**Scenario**: The factory is given a context with `--voices-dir` naming a folder with voice files.

**Expected**: The created adapter enumerates the voices in that folder.

##### SpeechModelCatalogAdapter_DamagedVoicesDirFile_DoesNotBreakAdapterOrListing

**Scenario**: The voices folder contains a file that cannot be decoded.

**Expected**: The adapter still composes and the voice listing still works.

##### SpeechModelCatalogAdapter_VoicePackUninstallAndInstall_UnknownOrMissingPack_BehaveCleanly

**Scenario**: A pack that was never installed is uninstalled, and an unknown pack is requested, against a
real store.

**Expected**: Uninstalling a never-installed pack is a no-op; an unknown pack id is reported cleanly.
This is the only test of a never-installed pack against a real store; the command tests use a fake.

##### SpeechModelCatalogAdapter_CompositionFails_PropagatesAndReleasesPartialCatalog

**Scenario**: A voices directory containing a NUL character makes composition fail after the catalog was
built.

**Expected**: The construction throws, and the store root is still usable afterwards (a second adapter
over the same root enumerates models). The test cannot observe disposal of the abandoned catalog
directly, so it verifies the failure path and the absence of a lock on the store root.
