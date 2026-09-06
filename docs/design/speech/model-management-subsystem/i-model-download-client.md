### IModelDownloadClient

**Purpose**: Define a mockable seam for fetching one downloadable file's bytes to a local
destination stream, isolating `SpeechModelDownloader` from any real HTTP client so its
queueing/verification/atomic-swap orchestration logic can be unit tested without any real network
access.

**Data Model**: No fields or properties; a pure behavioral contract.

**Key Methods**:

- **DownloadAsync(sourceUri, destination, progress, cancellationToken)**: Downloads the bytes at
  `sourceUri`, writing them to `destination` as they arrive, reporting `FileIndex = 0` /
  `FileCount = 1` progress (the caller rewrites these for multi-file context), and observing
  `cancellationToken` between chunks.

**Error Handling**: The interface itself defines no error handling beyond its documented
contract: implementations must throw (rather than silently truncate) on any non-success response
or transport failure, so a caller can distinguish a verified download from a partial or failed
one without inspecting `destination`'s length itself.

**Dependencies**: `SpeechModelDownloadProgress`.

**Callers**: `SpeechModelDownloader` (via its injected implementation); `HttpModelDownloadClient`
(implements it).
