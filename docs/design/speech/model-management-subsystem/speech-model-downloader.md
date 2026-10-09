### SpeechModelDownloader

**Purpose**: Orchestrate a single model's download end to end: fetching its declared file(s)
through an `IModelDownloadClient`, verifying each against its declared SHA-256 checksum, and
handing a fully verified result to `SpeechModelStore` for atomic install.

**Data Model**: A private `ConcurrentDictionary<string, SemaphoreSlim>` keyed by model id,
resolving (via `GetOrAdd`) a separate per-model-id lock for each distinct model id ever
downloaded through this instance. Two concurrent `DownloadAsync` calls for the same model id
serialize against that model id's own semaphore; calls for different model ids resolve to
different semaphores and so run fully concurrently, with no pool limit. `SpeechModelDownloadOutcome`
(declared alongside it) is an enum of `Installed`, `ChecksumMismatch`, `Failed`, `NetworkBlocked`,
`HttpError`, and `IoFailure` - deliberately distinct from the Sub-phase 2b model-catalog state
enum, since this type only ever describes one `DownloadAsync` call's outcome. The three new
members (appended after `Failed` to preserve any existing numeric-ordinal usage) let a host
distinguish *why* a non-checksum download/install failure occurred - a blocked/intercepted network
path, a real HTTP error response, or a local disk/permission failure - without inspecting `Error`'s
exception type itself; `Failed` remains the final generic fallback for anything unmatched.
`SpeechModelDownloadResult` (also declared alongside it) pairs an outcome with an optional
underlying exception, populated for every non-`Installed` outcome.

**Key Methods**:

- **SpeechModelDownloader(store, client?, diagnostics?)**: The original three-parameter
  constructor, kept as a distinct overload rather than widened. Accepts the store to install
  into, an optional injected `IModelDownloadClient` (creating and owning a default
  `HttpModelDownloadClient` when none is supplied), and an optional diagnostics sink. Forwards to
  the four-parameter overload below with `options: null`, so its behavior is byte-for-byte
  identical to before `SpeechModelDownloaderOptions` existed.
- **SpeechModelDownloader(store, client?, diagnostics?, options?)**: A distinct, additive
  overload (not an appended optional fourth parameter on the constructor above) that also accepts
  `SpeechModelDownloaderOptions?`. Declared as a separate overload rather than a widened original
  constructor because an appended optional parameter would still be *binary*-incompatible: an
  existing compiled caller's call site to the three-argument constructor is bound to that exact
  constructor's metadata token, which would no longer exist if a fourth parameter were added to
  it directly. None of this overload's four parameters carry a default value, so a one-, two-,
  or three-argument call can only ever resolve to the original overload above, never this one -
  keeping both source- and binary-compatible for every pre-existing call shape. A `null` `options`
  or a `null` `options.Mirror` (both the default) preserve this type's exact pre-mirror behavior;
  a non-null `Mirror` is retained and consulted by `ResolveEffectiveUri`, and is also passed
  through to the internally-created default `HttpModelDownloadClient` (when `client` is not
  supplied) so every request it issues carries that mirror's authentication.
- **ResolveEffectiveUri(mirror, modelId, file)** *(internal, directly unit-testable)*: Computes
  the actual request URI passed to `IModelDownloadClient.DownloadAsync` for one declared file.
  With no mirror configured, returns `file.Uri` completely unchanged - byte-for-byte identical to
  this type's behavior before the mirror feature existed. With a mirror configured, returns
  `{mirror.BaseUri}/{modelId}/{file.RelativeInstallPath}`, combined via `Uri`-segment
  construction that normalizes exactly one separating slash regardless of whether
  `mirror.BaseUri` itself ends in a trailing slash, and percent-escapes `modelId` and each
  `RelativeInstallPath` segment individually (so a subdirectory such as `tokens/vocab.txt`
  still resolves as two escaped segments, not one single escaped string containing a literal
  slash).
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
offending file's relative install path. Any other exception - raised either by the transport fetch
or by a model's own `InstallAsync` hook - is first checked by a guarded
`catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)` clause that
rethrows a genuine *caller* cancellation completely unchanged, never folding it into a result; only
once that guard has ruled out caller cancellation is every other exception passed to a dedicated,
directly unit-testable `ClassifyFailure(exception)` helper, which maps it to the most specific
outcome it recognizes: `HttpError` for an `HttpRequestException` with a non-null `StatusCode` (a
real HTTP response was received, e.g. 404/403/5xx); `NetworkBlocked` for an `HttpRequestException`
wrapping an `AuthenticationException` (a TLS/certificate failure - the shape produced by
TLS-interception middleboxes) or a `SocketException` (DNS failure, connection refused, host
unreachable), or for a private `HttpFetchTimeoutException` (an internal HTTP request timeout -
see below); `IoFailure` for an `IOException` or `UnauthorizedAccessException` (disk full,
permission denied, path too long); and `Failed` as the final generic fallback for anything else,
with the original exception always attached. Classification itself never throws - an unrecognized
exception type simply falls through to `Failed`, exactly as every exception did before this
feature existed.

`FetchFileAsync` wraps a `TaskCanceledException` thrown by the download client's `DownloadAsync`
call in a private `HttpFetchTimeoutException` only when all three guard conditions hold: the
client is the concrete `HttpModelDownloadClient`, `cancellationToken` itself was not canceled, and
the exception's `InnerException` is a `TimeoutException` - the documented shape .NET's `HttpClient`
produces specifically when its own `Timeout` elapses. Together these guards prove the exception can
only be `HttpClient`'s own internal request timeout, never a genuine caller cancellation (which is
always rethrown unchanged by the guard above). This wrapper exists purely so `ClassifyFailure`'s
match narrowly recognizes only this specific, HTTP-fetch-originated shape as `NetworkBlocked`,
without also misclassifying an unrelated `TaskCanceledException` thrown by
`ISpeechModel.InstallAsync`, by a host-injected `IModelDownloadClient`, or even by a
caller-supplied `HttpClient` whose own custom handler cancels a request for some other,
non-timeout reason; any such `TaskCanceledException` that does not satisfy every guard falls
through unwrapped to `ClassifyFailure`'s generic `Failed` fallback instead. The wrapper type itself
is never thrown or caught outside this class, and is always already unwrapped (as
`ClassifyFailure` never re-exposes it) before a result reaches a caller.

`SpeechModelDownloadResult.Error` is therefore
always populated for any non-`Installed` outcome, never left `null` for a caller to have to
special-case. Nothing is ever marked `Installed` for a corrupted, partial, checksum-mismatched, or
install-hook-failed download; an install-hook failure is classified through this same
`ClassifyFailure` logic exactly like a transport failure - the staging directory is discarded via
`SpeechModelStore.AbandonStaging` and any prior successful install of the same model is left
completely untouched. Staging-directory cleanup on these failure paths
(`AbandonStaging`/leftover-directory removal via `CleanUpLeftovers`) is best-effort: a process
crash or forced termination between a failed fetch and cleanup running can still leave an orphaned
staging directory behind; this is an accepted, documented limitation rather than a defect, since a
normal (non-crashed) failure path always cleans up.

**Dependencies**: `SpeechModelStore`, `IModelDownloadClient`, `HttpModelDownloadClient` (default),
`SpeechModelDownloadDescriptor`, `SpeechModelDownloadProgress`, `ISpeechDiagnostics`,
`ISpeechModel` (only for the `DownloadAsync(model, ...)` overload), `SpeechModelDownloaderOptions`,
`DownloadMirror`.

**Callers**: Hosts that need to download and install a model; `SpeechModelCatalog` (via the
`ISpeechModel`-aware overload).
