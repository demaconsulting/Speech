### SpeechModelDownloader

**Purpose**: Orchestrate a single model's download end to end: fetching its declared file(s)
through an `IModelDownloadClient`, verifying each against its declared SHA-256 checksum, and
handing a fully verified result to `SpeechModelStore` for atomic install.

**Data Model**: A private `ConcurrentDictionary<string, SemaphoreSlim>` keyed by model id,
resolving (via `GetOrAdd`) a separate per-model-id lock for each distinct model id ever
downloaded through this instance. Two concurrent `DownloadAsync` calls for the same model id
serialize against that model id's own semaphore; calls for different model ids resolve to
different semaphores and so run fully concurrently, with no pool limit. `SpeechModelDownloadOutcome`
(declared alongside it) is an enum of `Installed`, `ChecksumMismatch`, and `Failed` - deliberately
distinct from the Sub-phase 2b model-catalog state enum, since this type only ever describes one
`DownloadAsync` call's outcome. `SpeechModelDownloadResult` (also declared alongside it) pairs an
outcome with an optional underlying exception when the outcome is `Failed`.

**Key Methods**:

- **SpeechModelDownloader(store, client?, diagnostics?)**: Accepts the store to install into, an
  optional injected `IModelDownloadClient` (creating and owning a default
  `HttpModelDownloadClient` when none is supplied), and an optional diagnostics sink.
- **DownloadAsync(modelId, descriptor, progress?, cancellationToken)**: Acquires this model id's
  own lock (queueing only behind another in-flight download of the *same* model id - a download
  of a different model id proceeds immediately in parallel). Immediately after acquiring that
  lock - and before touching the network or the staging directory at all - checks
  `SpeechModelStore.IsInstalled(modelId)`: if the model is already installed, opportunistically
  calls `SpeechModelStore.CleanUpLeftovers(modelId)` (a cheap, best-effort directory
  enumeration/delete with no network or hashing involved, so it does not compromise the no-op
  contract below), reports an `Info` diagnostic ("Model '{id}' is already installed; DownloadAsync
  is a no-op.") and returns `SpeechModelDownloadResult(SpeechModelDownloadOutcome.Installed)`
  immediately, with no fetch, verification, or network work performed at all - the only
  staging-directory activity is the cheap opportunistic leftover sweep. This fast-path check
  is deliberately placed
  *inside* the per-model-id lock (never before acquiring it) so it stays race-safe against a
  concurrent first-time install of the same model id: two callers racing to install the same
  not-yet-installed id still serialize on the lock as before, and only a caller that genuinely
  observes the model already installed (whether it always was, or became so while queued behind
  another caller's install) takes the fast path - it can never observe a half-installed state.
  This makes `DownloadAsync` for an already-installed model id a true no-op, safe for a host to
  call unconditionally on every launch; once a model is installed there is no way through this
  public API to force a re-verification/repair of it (a host that wants to force a fresh
  install must first uninstall the model). When the model is not yet installed, proceeds to
  begin staging via `SpeechModelStore`, fetches each declared file in order (rewriting each
  file's progress reports with its true `FileIndex`/`FileCount` via an internal,
  order-preserving `RelayProgress` adapter), verifies its SHA-256 checksum, and - only once
  every file verifies - hands the staging
  directory to `SpeechModelStore.CompleteInstall`. A checksum mismatch, transport failure, or
  cancellation abandons the staging directory via `SpeechModelStore.AbandonStaging` and leaves any
  prior successful install of the same model untouched. This overload never invokes any model's
  `InstallAsync` hook (it has no `ISpeechModel` reference to call it on) - its signature and
  behavior are completely unchanged by the addition of the overload below.
- **DownloadAsync(model, progress?, cancellationToken)**: An additive overload taking an
  `ISpeechModel` instead of a bare `modelId`/`descriptor` pair. Acquires the same per-model-id
  lock (keyed by `model.Id`) as the overload above, then runs the identical fetch/verify sequence
  against `model.DownloadDescriptor`, then - after every file is verified and before
  `SpeechModelStore.CompleteInstall` runs - invokes `model.InstallAsync(stagingDirectory,
  cancellationToken)`, letting the model unpack an archive or otherwise process its own staged
  files in place. The installed size recorded in the manifest is then recomputed by summing the
  staging directory's actual post-install file tree, so `TotalSizeBytes` reflects what was
  genuinely installed even when a model expands or shrinks during unpacking. `SpeechModelCatalog`
  uses this overload exclusively, so every catalog-driven download genuinely runs the model's
  install hook.

**Error Handling**: A checksum mismatch returns `SpeechModelDownloadOutcome.ChecksumMismatch` with
`Error` populated with a descriptive `InvalidOperationException` naming the model id and the
offending file's relative install path; an `IOException`/`HttpRequestException`/
`UnauthorizedAccessException`/`InvalidDataException` - raised either by the transport fetch or by
a model's own `InstallAsync` hook - returns `SpeechModelDownloadOutcome.Failed` with the
underlying exception attached. `SpeechModelDownloadResult.Error` is therefore always populated for
any non-`Installed` outcome, never left `null` for a caller to have to special-case. Cancellation
propagates as `OperationCanceledException` (not folded into a result) after best-effort staging
cleanup. Nothing is ever marked `Installed` for a corrupted, partial, checksum-mismatched, or
install-hook-failed download; an install-hook failure is handled identically to a transport
failure - the staging directory is discarded via `SpeechModelStore.AbandonStaging` and any prior
successful install of the same model is left completely untouched. Staging-directory cleanup on
these failure paths (`AbandonStaging`/leftover-directory removal via `CleanUpLeftovers`) is
best-effort: a process crash or forced termination between a failed fetch and cleanup running can
still leave an orphaned staging directory behind; this is an accepted, documented limitation
rather than a defect, since a normal (non-crashed) failure path always cleans up.

**Dependencies**: `SpeechModelStore`, `IModelDownloadClient`, `HttpModelDownloadClient` (default),
`SpeechModelDownloadDescriptor`, `SpeechModelDownloadProgress`, `ISpeechDiagnostics`,
`ISpeechModel` (only for the `DownloadAsync(model, ...)` overload).

**Callers**: Hosts that need to download and install a model; `SpeechModelCatalog` (via the
`ISpeechModel`-aware overload).
