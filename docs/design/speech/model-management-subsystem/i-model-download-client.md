### IModelDownloadClient

**Purpose**: Define a mockable seam for fetching one downloadable file's bytes to a local
destination stream, isolating `SpeechModelDownloader` from any real HTTP client so its
queueing/verification/atomic-swap orchestration logic can be unit tested without any real network
access.

**Data Model**: No fields or properties; a pure behavioral contract.

**Key Methods**:

- **DownloadAsync(sourceUri, destination, progress, cancellationToken, mirrorAuth)**: Downloads
  the bytes at `sourceUri`, writing them to `destination` as they arrive, reporting
  `FileIndex = 0` / `FileCount = 1` progress (the caller rewrites these for multi-file context),
  and observing `cancellationToken` between chunks. `mirrorAuth` is an optional `DownloadMirror`
  (defaulted to `null` for every existing caller and implementation) carrying the
  authentication, if any, that this specific request's implementation should apply - typically
  supplied only when `sourceUri` was itself resolved beneath that same mirror. An implementation
  must not need to know about `ISpeechModel`/model id/catalog concepts to honor it.

**Error Handling**: The interface itself defines no error handling beyond its documented
contract: implementations must throw (rather than silently truncate) on any non-success response
or transport failure, so a caller can distinguish a verified download from a partial or failed
one without inspecting `destination`'s length itself.

**Dependencies**: `SpeechModelDownloadProgress`, `DownloadMirror`.

**Callers**: `SpeechModelDownloader` (via its injected implementation); `HttpModelDownloadClient`
(implements it).
