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

The unit passes when all of the following hold:

- It forwards enumeration and download to the library without adding or hiding anything, and
  never disposes a catalog it does not own.
- `ModelInstalled` fires exactly once with the correct model id and role for a genuine install
  and never for a failed, canceled or checksum-mismatched download.
- A factory-constructed instance rebuilds and takes ownership of its catalog when the mirror
  changes, keeps the previous catalog when the factory fails, and one built without a factory
  rejects reconfiguration.
- The reference-voice members report the library's packs, rebuild the catalog and library pair,
  and raise `ReferenceVoicesChanged` however an install ends, and an install's own exception is
  never replaced by a rebuild failure.
- An instance without a voice library reports no packs and refuses pack operations.
- An uninstall that finds an install in flight waits for it and then removes the pack, rebuilding
  and raising once; a failed uninstall neither rebuilds nor raises.
- Disposing a composed service disposes both the catalog and the voice library.

#### Test Scenarios

##### ModelCatalogService_Constructor_NullCatalog_ThrowsArgumentNullException

**Scenario**: The adapter is constructed with no library catalog.

**Expected**: `ArgumentNullException`.

##### ModelCatalogService_Enumerate_LibraryKnowsRealModels_ReturnsThem

**Scenario**: The adapter enumerates the real library catalog.

**Expected**: The same models the library itself knows about, reported as-is with each model's
own declared role preserved rather than replaced with invented placeholder models.

##### ModelCatalogService_DownloadAsync_LibraryModel_InstallsThroughLibraryCatalog

**Scenario**: A download is requested for a model known only to an injected library catalog, served
by a fake `IModelDownloadClient`.

**Expected**: The download completes as installed and the library catalog itself reports the model
as downloaded, proving the download is carried out by the library.

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

##### ModelCatalogService_DownloadAsync_ChecksumMismatch_DoesNotRaiseModelInstalled

**Scenario**: A download serves bytes that do not match the model's declared checksum.

**Expected**: The library reports a checksum mismatch and `ModelInstalled` is never raised.

##### ModelCatalogService_DownloadAsync_Canceled_DoesNotRaiseModelInstalled

**Scenario**: The caller cancels a download while the fake download client is transferring.

**Expected**: The cancellation propagates to the caller and `ModelInstalled` is never raised.

##### ModelCatalogService_Constructor_NullCatalogFactory_ThrowsArgumentNullException

**Scenario**: The factory-based constructor is given a catalog but a null catalog factory.

**Expected**: `ArgumentNullException`.

##### ModelCatalogService_ApplyMirror_NoFactory_ThrowsInvalidOperationException

**Scenario**: `ApplyMirror` is called on an adapter built without a factory.

**Expected**: `InvalidOperationException`, so a service that cannot be reconfigured says so rather
than silently doing nothing.

##### ModelCatalogService_ApplyMirror_WithFactory_RebuildsCatalogThroughFactory

**Scenario**: `ApplyMirror` is called on an adapter built with a catalog factory, first with a
mirror and then with `null`.

**Expected**: The factory is invoked with downloader options carrying the requested mirror, then
with no options at all, and subsequent enumeration reflects the rebuilt catalog.

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

##### ModelCatalogService_ReferenceVoices_NoLibrary_ReportsNoPacks

**Scenario**: A service built without a voice library is asked for its packs and user voices folder.

**Expected**: No packs and no user voices folder are reported.

##### ModelCatalogService_ReferenceVoices_NoLibrary_RefusesPackStateRequest

**Scenario**: A service built without a voice library is asked for a pack's state.

**Expected**: `InvalidOperationException`.

##### ModelCatalogService_ReferenceVoices_NoLibrary_RefusesEmotionsRequest

**Scenario**: A service built without a voice library is asked whether a pack has emotion variants.

**Expected**: `InvalidOperationException`.

##### ModelCatalogService_ReferenceVoices_NoLibrary_RefusesUninstallRequest

**Scenario**: A service built without a voice library is asked to uninstall a pack.

**Expected**: `InvalidOperationException`.

##### ModelCatalogService_ReferenceVoices_Composed_ListsPack

**Scenario**: A composed service is asked for its voice packs.

**Expected**: The library's pack is reported as not installed.

##### ModelCatalogService_ReferenceVoices_Composed_ReportsUserVoicesDirectory

**Scenario**: A composed service is asked for its user voices folder.

**Expected**: A folder inside the catalog's store root is reported.

##### ModelCatalogService_ReferenceVoices_Composed_NotInstalledPackHasNoEmotions

**Scenario**: A composed service is asked whether a pack that is not installed has emotion variants.

**Expected**: `false`.

##### ModelCatalogService_InstallReferenceVoicePackAsync_Success_RebuildsAndRaises

**Scenario**: A pack install succeeds through a composed service.

**Expected**: The pair is rebuilt and `ReferenceVoicesChanged` is raised.

##### ModelCatalogService_InstallReferenceVoicePackAsync_SuccessThenRebuildFails_Propagates

**Scenario**: A pack install succeeds and the composition factory then throws.

**Expected**: The rebuild's exception propagates.

##### ModelCatalogService_UninstallReferenceVoicePackAsync_Fails_DoesNotRebuildOrRaise

**Scenario**: The library's uninstall throws.

**Expected**: The exception propagates unchanged, and there is no rebuild and no
`ReferenceVoicesChanged`.

##### ModelCatalogService_ApplyMirror_Composed_FactoryFails_KeepsPreviousPair

**Scenario**: The composition factory throws during `ApplyMirror`.

**Expected**: The exception propagates and the previous pair stays published and undisposed.

##### ModelCatalogService_Dispose_Composed_DisposesCatalogAndVoiceLibrary

**Scenario**: A composed service is disposed.

**Expected**: Both the catalog and the voice library are disposed.

##### ModelCatalogComposition_Constructor_NullMember_ThrowsArgumentNullException

**Scenario**: A composition is constructed with a null catalog or a null library.

**Expected**: `ArgumentNullException`.

##### ModelCatalogService_UninstallReferenceVoicePack_RebuildsCatalogAndRaisesEvent

**Scenario**: A pack is uninstalled through a composed service.

**Expected**: The pair is rebuilt, the old voice library is disposed (a later install through it
throws `ObjectDisposedException`), and `ReferenceVoicesChanged` is raised exactly once.

##### ModelCatalogService_ApplyMirror_Composed_RebuildsPairAndRaisesEvent

**Scenario**: The mirror is changed on a composed service.

**Expected**: The pair is rebuilt with the new options, the old voice library is disposed, and
`ReferenceVoicesChanged` is raised.

##### ModelCatalogService_InstallReferenceVoicePackAsync_Failure_StillRebuildsAndRaises

**Scenario**: A pack install fails against an unreachable loopback mirror.

**Expected**: The pair is still rebuilt and `ReferenceVoicesChanged` raised, because an upgrade may
have changed the pack's installed state.

##### ModelCatalogService_InstallReferenceVoicePackAsync_Canceled_StillRebuildsAndRaises

**Scenario**: A pack install is canceled.

**Expected**: The pair is still rebuilt and `ReferenceVoicesChanged` raised.

##### ModelCatalogService_InstallReferenceVoicePackAsync_RebuildFailsAfterCancel_RethrowsCancellation

**Scenario**: A pack install is canceled and the composition factory then throws during the rebuild.

**Expected**: The cancellation propagates, not the rebuild's exception.

##### ModelCatalogService_UninstallReferenceVoicePackAsync_InstallInFlight_WaitsThenUninstalls

**Scenario**: A pack is uninstalled while an install of that pack is in flight.

**Expected**: The uninstall waits for the install (no rebuild and no `ReferenceVoicesChanged` while it
runs), then removes the pack and rebuilds and raises the event once more.

#### Accepted Gaps

- **Throwing disposal during a rebuild**: `Recompose` disposes the previous library even when
  disposing the previous catalog throws (a `try`/`finally`). The library's `SpeechModelCatalog` and
  `ReferenceVoiceLibrary` are sealed and their disposal cannot be made to throw through any public
  seam, so this ordering is verified by inspection only.
- **Unknown role after install**: `ModelInstalled` is skipped when the installed model's role cannot
  be found in the enumeration. A model the catalog just installed is always enumerated with a role,
  so the branch is purely defensive and cannot be reached through the library's public API.
