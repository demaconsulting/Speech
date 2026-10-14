### IModelDownloadClient

**Purpose**: Define a mockable seam for fetching one downloadable file's bytes to a local
destination stream, isolating `SpeechModelDownloader` from any real HTTP client so its
fetch/verify/install orchestration logic can be unit tested without any real network
access.

**Data Model**: No fields or properties; a pure behavioral contract.

**Key Methods**:

- **DownloadAsync(sourceUri, destination, progress, cancellationToken)**: Downloads the bytes at
  `sourceUri`, writing them to `destination` as they arrive, reporting `FileIndex = 0` /
  `FileCount = 1` progress (the caller rewrites these for multi-file context), and observing
  `cancellationToken` between chunks. The caller owns `destination`'s lifetime: an implementation
  writes to it but never disposes it, and on success it holds exactly the fetched bytes. This
  signature has exactly four parameters, is part of this library's public API surface, and is
  intentionally never widened with a per-call mirror-authentication parameter (an earlier
  five-parameter form carrying `mirrorAuth` no longer exists), so a host's
  own `IModelDownloadClient` implementation stays source-compatible across releases; any mirror
  authentication an implementation applies is configured once, at construction (see
  `HttpModelDownloadClient`'s constructor), never per call. `SpeechModelDownloader`'s own
  contribution toward mirror support is resolving `sourceUri` to its mirror-relative effective
  value (or leaving it unchanged when no mirror is configured) before calling `DownloadAsync` -
  see `SpeechModelDownloader.ResolveEffectiveUri`.

**Argument contract**: `sourceUri` and `destination` must not be null (`ArgumentNullException`); only `progress`
may be null. `sourceUri` must be an absolute HTTP(S) URI; an implementation rejects a relative or other-scheme
URI by throwing, and the exception type is implementation-defined. `destination` must be writable
(`CanWrite`); the client appends sequentially from its current position and never closes, seeks or disposes
it. After a failure or cancellation it may hold a partial prefix of the file.

**Error Handling**: Implementations must throw (rather than silently truncate) on any non-success response
or transport failure: `HttpRequestException` for an HTTP-based implementation such as
`HttpModelDownloadClient`, or another failure exception (for example `IOException`) for other
implementations. Cancellation of `cancellationToken` surfaces as an `OperationCanceledException`, so a caller
can distinguish a verified download from a partial or failed one without inspecting `destination`'s length.

**Dependencies**: `SpeechModelDownloadProgress` (carried by the `IProgress<SpeechModelDownloadProgress>`
parameter), plus the BCL types `Uri`, `Stream`, `IProgress<T>` and `CancellationToken`.

**Requirement mapping**: the unit requirements state the observable contract only (substitutable
without network, construction-time-only authentication, exact byte delivery with the destination left
open, single-file progress, exception on failure, cancellation surfacing); the member signature, the
four-parameter shape and the source-compatibility rationale above are design detail.

**Callers**: `SpeechModelDownloader` (via its injected implementation); `HttpModelDownloadClient`
(implements it).
