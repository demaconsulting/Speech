### ClusterCorpusCache Verification

#### Verification Approach

`ClusterCorpusCache` and the source list are verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/ClusteringSubsystem/ClusterCorpusCacheTests.cs`. The tests use the
clustering fixture (`Fakes/ClusteringFixture.cs`): a temporary data folder with a source list written in code and three
generated books served by a fake mirror. The network and the pause are the injected dependencies: the fetcher delegate
answers from memory (or with a configured HTTP status, or a test-specific failure sequence) and records every requested
address, and the sleeper records every pause, so no test waits or reaches the network. `GutenbergBody` and
`CorpusSnapshot` are used as they are.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test has its own temporary folder, deleted on disposal
- **Not used**: the network and any real book

#### Acceptance Criteria

**Source list.** A valid list parses with its rows in file order and its used books in ordinal order; a main-site
address, a plain-HTTP address, a pin on an excluded book and descending numbers are refused.

**Pins.** New pins fill only rows without one and change no other byte.

**Polite fetch.** Unpinned books are fetched (two requests each) with the delay before every request but the first and
only from the allowed host; HTTP 429 stops the run after one request; two transient failures are retried after 5 and
15 seconds; a missing file fails only its book without a retry; the mirror option replaces the host and a host off the
allow-list is refused.

**Verified bodies.** A pinned, cached corpus makes no request; a tampered body is refused at reading and restored from
the raw download without a request; a mirror text that differs from its pin fails the book and writes no body; an
unpinned book cannot be read; a snapshot is read only when each body matches its pin.

#### Test Scenarios

##### Source list and pins

**Tests**: `ClusterSourceList_Load_ValidList_ParsesRows`, `ClusterSourceList_Load_InvalidRows_Throw`,
`ClusterSourceList_WithPins_FillsOnlyUnpinnedRows`

Rows 9, 10 and 11 load in that order with the used books in ordinal order (11 before 9). Each defective list throws
`InvalidDataException`. With one pinned and one unpinned row, the pinned row keeps its pin and the new text equals the
old with only the unpinned row's two cells filled.

##### Polite fetch

**Tests**: `ClusterCorpusCache_Fetch_UnpinnedBooks_FetchesPoliteAndPins`,
`ClusterCorpusCache_Fetch_TooManyRequests_StopsRun`, `ClusterCorpusCache_Fetch_TransientFailure_RetriesWithBackoff`,
`ClusterCorpusCache_Fetch_NotFound_FailsBookWithoutRetry`, `ClusterCorpusCache_Mirror_ReplacesHostAndRefusesOthers`

Three unpinned books give six requests to the allowed host, five pauses of the configured two seconds and a pin equal to
the extracted body's. HTTP 429 throws `IOException` after one request. With two HTTP 503 failures the pauses are 5 s, 2
s, 15 s, 2 s and 2 s and the book is pinned after four calls. Without the text of one book that book fails after one
request for it and the others are pinned. With the mirror `aleph.gutenberg.org` every request goes there, and the host
`www.gutenberg.org` is refused with `ArgumentException`.

##### Verified bodies

**Tests**: `ClusterCorpusCache_Fetch_PinnedAndCached_MakesNoRequest`,
`ClusterCorpusCache_TamperedBody_RefusedThenRestored`, `ClusterCorpusCache_Fetch_MirrorTextDiffersFromPin_Fails`,
`ClusterCorpusCache_ReadBodies_UnpinnedBook_Throws`, `ClusterCorpusCache_ReadBodiesFromSnapshot_VerifiesPins`

After pinning, a second fetch reports every book cached with no request. A body with appended text is refused by
`ReadBodies`, and the next fetch restores it from the raw file without a request. A pin that no download matches makes
the book fail with a message about the pin and no body file. Reading unpinned bodies throws and says to write the pins.
A snapshot of the pinned bodies reads back in ordinal order; one without a book and one with a changed body are refused.
