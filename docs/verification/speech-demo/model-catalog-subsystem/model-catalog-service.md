### ModelCatalogService Verification

#### Verification Approach

`ModelCatalogService` is verified through deterministic unit tests against a real
`SpeechModelCatalog` rooted in an isolated scratch directory, proving the adapter really forwards
to the library, plus injected fake models and a fake `IModelDownloadClient` so a genuine install
can be proven with no network access. The reference-voice members are verified against composed
catalog and `ReferenceVoiceLibrary` pairs built by a test factory.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test roots the library's model store in a fresh directory under the test
  output folder, so no test touches a developer's real installed-model store
- **Test doubles**: A fake `ISpeechModel` and a fake `IModelDownloadClient` injected directly into
  the library's `SpeechModelCatalog`

#### Acceptance Criteria

The unit passes when: it forwards enumeration and download to the library without adding or
hiding anything and never disposes a catalog it does not own; `ModelInstalled` fires exactly once
with the correct model id and role for a genuine install and never for a failed download; a
factory-constructed instance rebuilds and takes ownership of its catalog when the mirror changes
while one built without a factory rejects reconfiguration; and the reference-voice members report
the library's packs, rebuild the catalog and library pair, and raise `ReferenceVoicesChanged`
however an operation ends, while an instance without a voice library reports no packs and refuses
pack operations.

#### Test Scenarios

##### ModelCatalogService_Constructor_NullCatalog_ThrowsArgumentNullException

**Scenario**: The adapter is constructed with no library catalog.

**Expected**: `ArgumentNullException`.

##### ModelCatalogService_Enumerate_LibraryKnowsRealModels_ReturnsThem

**Scenario**: The adapter enumerates the real library catalog.

**Expected**: The same models the library itself knows about, reported as-is with each model's
own declared role preserved rather than replaced with invented placeholder models.

##### ModelCatalogService_DownloadAsync_UnknownModelId_ThrowsArgumentException

**Scenario**: A download is requested for a model id the library does not know.

**Expected**: The library's own `ArgumentException` propagates, proving the adapter delegates
rather than reimplements.

##### ModelCatalogService_Enumerate_AfterAnotherAdapterFallsOutOfUse_StillWorks

**Scenario**: A second adapter over the same catalog goes out of use and the first enumerates.

**Expected**: Enumeration still succeeds, proving an adapter never disposes a catalog it does not
own.

##### ModelCatalogService_DownloadAsync_Installed_RaisesModelInstalledWithCorrectIdAndRole

**Scenario**: A download completes with the library genuinely installing an injected fake model (a
fake `IModelDownloadClient` serves a payload whose checksum matches the declared descriptor).

**Expected**: `ModelInstalled` fires exactly once, carrying the installed model's id and its
declared role.

##### ModelCatalogService_DownloadAsync_Failed_DoesNotRaiseModelInstalled

**Scenario**: A download fails against an injected fake model and a client that always throws.

**Expected**: `ModelInstalled` is never raised.

##### ModelCatalogService_Constructor_NullCatalogFactory_ThrowsArgumentNullException

**Scenario**: The factory-based constructor is given a catalog but a null catalog factory.

**Expected**: `ArgumentNullException`.

##### ModelCatalogService_ApplyMirror_NoFactory_ThrowsInvalidOperationException

**Scenario**: `ApplyMirror` is called on an adapter built without a factory.

**Expected**: `InvalidOperationException`, so a service that cannot be reconfigured says so rather
than silently doing nothing.

##### ModelCatalogService_ApplyMirror_WithFactory_RebuildsCatalogThroughFactory

**Scenario**: `ApplyMirror` is called on an adapter built with a catalog factory.

**Expected**: The factory is invoked with downloader options carrying the requested mirror, and
subsequent enumeration reflects the rebuilt catalog.

##### ModelCatalogService_ApplyMirror_WithFactory_DisposesReplacedCatalog

**Scenario**: `ApplyMirror` is called after a prior download has seeded a per-model lock.

**Expected**: The replaced catalog is genuinely disposed, proven by a later direct download
attempt against it surfacing `ObjectDisposedException`.

##### ModelCatalogService_Dispose_WithFactory_DisposesCurrentCatalog

**Scenario**: An adapter built with a catalog factory is disposed.

**Expected**: The current catalog is genuinely disposed.

##### ModelCatalogService_Dispose_WithoutFactory_DoesNotDisposeCatalog

**Scenario**: An adapter built without a factory is disposed.

**Expected**: The shared catalog is left usable.

##### ModelCatalogService_Constructor_NullComposition_ThrowsArgumentNullException

**Scenario**: The composition constructor is given a null composition.

**Expected**: `ArgumentNullException`.

##### ModelCatalogService_ReferenceVoices_NoLibrary_ReportsNoPacksAndRefusesOperations

**Scenario**: A service built without a voice library is asked for packs and for pack operations.

**Expected**: No packs and no user voices folder are reported, and the state, install and uninstall
members throw `InvalidOperationException`.

##### ModelCatalogService_ReferenceVoices_Composed_ListsPackAndUserDirectory

**Scenario**: A composed service is asked for its voice packs and user voices folder.

**Expected**: The library's pack and the user voices folder are reported.

##### ModelCatalogService_UninstallReferenceVoicePack_RebuildsCatalogAndRaisesEvent

**Scenario**: A pack is uninstalled through a composed service.

**Expected**: The pair is rebuilt, the old one disposed, and `ReferenceVoicesChanged` raised
exactly once.

##### ModelCatalogService_ApplyMirror_Composed_RebuildsPairAndRaisesEvent

**Scenario**: The mirror is changed on a composed service.

**Expected**: The pair is rebuilt and `ReferenceVoicesChanged` raised.

##### ModelCatalogService_InstallReferenceVoicePackAsync_Failure_StillRebuildsAndRaises

**Scenario**: A pack install fails against an unreachable loopback mirror.

**Expected**: The pair is still rebuilt and `ReferenceVoicesChanged` raised, because an upgrade may
have changed the pack's installed state.

##### ModelCatalogService_InstallReferenceVoicePackAsync_Canceled_StillRebuildsAndRaises

**Scenario**: A pack install is canceled.

**Expected**: The pair is still rebuilt and `ReferenceVoicesChanged` raised.
