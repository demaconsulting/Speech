### ClusterCorpusCache

**Purpose**: Hold the source list of the word-cluster corpus, fetch each used book from an official mirror with a polite
rate limit, pin and verify its normalized body and hand out only bodies that match their pins. It is the only unit that
reaches the network or opens a file of the corpus cache.

**Data Model** (internal types):

- `ClusterSource`: one row of `cluster-sources.tsv`: line, id `gutenberg-N`, number `N`, group, title, author, year,
  URL, license basis, basis reference, body pin (SHA-256 and size, or none) and status; `IsUsed` and `IsPinned`
- `ClusterSourceList`: the file bytes (the record pins them), their SHA-256, the rows in file order and the used rows in
  ordinal id order (`UsedInCorpusOrder`); `Load` and `WithPins`
- `ClusterCorpusCache`: the cache folder, the **fetcher** (a delegate that opens a URL as a stream; the default
  `GutenbergMirrorFetcher` sends an identifying user agent, accepts only HTTPS on an allowed mirror, uses a handler with
  automatic redirects off (`CreateHandler`), turns any 3xx answer into an `InvalidDataException` (that book fails,
  without a retry) and any other non-success status into an `HttpRequestException` with the status; the overload
  `Open(HttpClient, uri, token)` is the seam the tests answer through a fake handler, and `IsAllowedMirror` is the one
  HTTPS-and-allowed-host rule that the fetcher and `Download` share), the **sleeper** (a delegate that pauses; by
  default the pause waits on the caller's cancellation token, so a cancellation ends it at once), the delay and the
  optional mirror host; the number of requests made. Not thread-safe
- `CorpusFetchResult`: per used book, the outcome `cached`, `fetched`, `pinned` (a new pin was computed) or `failed`,
  the problem and the body

**The source list.** The header is `source_id`, `group`, `title`, `author`, `year`, `url`, `license_basis`,
`basis_ref`, `body_sha256`, `body_bytes`, `status`. `Load` reads it with `TsvTable` (UTF-8 without byte order mark, LF,
NFC, constant columns) and rejects it unless the ids are `gutenberg-N` with `N` strictly ascending; the group is one of
`core-open`, `core-silver`, `domain-anatomy`, `domain-textiles`, `domain-sport`, `domain-music`, `domain-clocks`; the
status is one of `used`, `excluded-dev`, `excluded-holdout`, `rejected-license`, `rejected-content`; the year is at
most 1929 or `-`; title, author, basis and reference are not empty; the URL is HTTPS on an allowed mirror (no query,
fragment or port) and ends with `/N/pgN.txt`, `/N/N-0.txt` or `/N/N.txt`; and a pin is `-` in both cells or, only on a
used row, a 64-digit lowercase SHA-256 and a positive size. Up to ten problems are reported together.

**The cache layout.** `raw/pgN.txt` and `raw/pgN.rdf` as downloaded, `bodies/gutenberg-N.txt` the normalized body.
Every file is written to a `.partial` sibling and renamed, so a reader never sees a truncated file.

**Key Methods**:

- **ClusterSourceList.WithPins(pins)**: returns the file bytes with the two pin cells of each unpinned row in `pins`
  filled; every other byte is unchanged and a pinned row is never changed
- **Fetch(sources, log, token)**: for each used book in ordinal id order: fetches the metadata if it is missing; does
  nothing more when the cached body matches its pin; otherwise reuses the raw download when it yields the pinned body
  (or when there is no pin yet) and fetches the text again only when it is missing or does not; extracts the body with
  `GutenbergBody`; a pinned book whose new body differs from its pin fails and nothing is written; otherwise the body is
  written. One line per book goes to the log
- **Download (private)**: refuses a URL that is not HTTPS on an allowed mirror; pauses for the delay before every
  request except the first of the run; on HTTP 403 or 429 throws `IOException` (the run stops); on HTTP 404 rethrows
  (the book fails); a redirect refused by the fetcher (`InvalidDataException`) is not retried and fails the book; on any
  other failure retries after 5, 15 and 45 seconds and then throws `IOException`; reads at most 64 MiB. A cancellation
  by the caller is never retried: an `OperationCanceledException` (such as the `TaskCanceledException` of a canceled
  HTTP request) is rethrown at once when the caller's token is canceled, and every pause checks the token before and
  after it, so a cancellation during a backoff stops the run before the next request. A `TaskCanceledException` while
  the caller's token is not canceled (the HTTP client's own timeout) is a transient failure and is retried
- **CheckBody(source)** and **ReadBodies(sources)**: verify each used book's cached body against its pin, without the
  network, and return the bodies in ordinal id order
- **ReadBodiesFromSnapshot(sources, path)**: reads the members of a snapshot with `CorpusSnapshot.Read` and returns each
  used book's body after verifying it against its pin

**Error Handling**: null arguments throw `ArgumentNullException`; a mirror off the allow-list or a negative delay throws
`ArgumentException`. An invalid list, an unpinned or missing body, a body that differs from its pin, and a snapshot that
lacks a body throw `InvalidDataException`. A refusal by the mirror or exhausted retries throw `IOException`. A
cancellation by the caller throws `OperationCanceledException`, never wrapped and never retried. The
default fetcher never follows a redirect: a 3xx answer throws `InvalidDataException`, because a redirect could lead from
the mirror to the main web site. Within `Fetch`, a book whose download or extraction fails (including a redirect)
becomes a `failed` result and the other books continue.

**Dependencies**: the .NET base class library (HTTP client, files, hashing), `GutenbergBody`, `CorpusSnapshot`
(reading a snapshot), and the TrainingDataSubsystem's `TsvTable` and `CheckReport`.

**Callers**: `ClusterCommand` and the unit tests.
