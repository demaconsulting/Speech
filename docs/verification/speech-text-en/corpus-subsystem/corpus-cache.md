### CorpusCache Verification

#### Verification Approach

`CorpusCache` and the source list are verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/CorpusSubsystem/CorpusCacheTests.cs`. The tests use the
corpus fixture (`Fakes/CorpusFixture.cs`): a temporary data folder with a source list written in code and three
generated books served by a fake mirror. The network and the pause are the injected dependencies: the fetcher delegate
answers from memory (or with a configured HTTP status, or a test-specific failure sequence) and records every requested
address, and the sleeper records every pause, so no test waits or reaches the network (one cancellation test uses the
default pause, which a cancellation ends after 50 ms). `GutenbergBody` is used as it is. The default fetcher's own HTTP
path is tested through its `GutenbergMirrorFetcher.Open(HttpClient, uri, token)` overload with a fake
`HttpMessageHandler` that answers from a callback and records every request (still no network); its production handler
is inspected directly.

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
allow-list is refused. The production handler does not follow redirects; a success answer returns the body and carries
the identifying user agent; an HTTP 302 to `www.gutenberg.org` is refused after one request without being followed;
within `Fetch`, a redirected book fails once, without a retry, and the other books are pinned.

**Verified bodies.** A pinned, cached corpus makes no request; a tampered body is refused at reading and restored from
the raw download without a request; a mirror text that differs from its pin fails the book and writes no body; an
unpinned book cannot be read.

**Cancellation.** A cancellation raised by the fetcher for a canceled caller propagates as `OperationCanceledException`
after one request and no pause; a cancellation during the first retry pause stops the run after that one pause, before
a second request; with the default pause, a cancellation 50 ms into the 5-second backoff ends the run well before 5
seconds; a `TaskCanceledException` without a canceled caller (a timeout) is retried after 5 seconds and the book is
pinned.

#### Test Scenarios

##### Source list and pins

**Tests**: `CorpusSourceList_Load_ValidList_ParsesRows`, `CorpusSourceList_Load_InvalidRows_Throw`,
`CorpusSourceList_WithPins_FillsOnlyUnpinnedRows`

Rows 9, 10 and 11 load in that order with the used books in ordinal order (11 before 9). Each defective list throws
`InvalidDataException`. With one pinned and one unpinned row, the pinned row keeps its pin and the new text equals the
old with only the unpinned row's two cells filled.

##### Polite fetch

**Tests**: `CorpusCache_Fetch_UnpinnedBooks_FetchesPoliteAndPins`,
`CorpusCache_Fetch_TooManyRequests_StopsRun`, `CorpusCache_Fetch_TransientFailure_RetriesWithBackoff`,
`CorpusCache_Fetch_NotFound_FailsBookWithoutRetry`, `CorpusCache_Mirror_ReplacesHostAndRefusesOthers`

Three unpinned books give six requests to the allowed host, five pauses of the configured two seconds and a pin equal to
the extracted body's. HTTP 429 throws `IOException` after one request. With two HTTP 503 failures the pauses are 5 s, 2
s, 15 s, 2 s and 2 s and the book is pinned after four calls. Without the text of one book that book fails after one
request for it and the others are pinned. With the mirror `aleph.gutenberg.org` every request goes there, and the host
`www.gutenberg.org` is refused with `ArgumentException`.

##### Redirects

**Tests**: `GutenbergMirrorFetcher_CreateHandler_Default_DoesNotFollowRedirects`,
`GutenbergMirrorFetcher_Open_Success_ReturnsBodyWithUserAgent`,
`GutenbergMirrorFetcher_Open_RedirectToMainSite_RefusedWithoutFollowing`,
`CorpusCache_Fetch_MirrorRedirects_FailsBookWithoutRetry`

The handler of the shared client has `AllowAutoRedirect` off. Through a fake handler, a 200 answer returns the body
bytes after one request that carries the identifying user agent. An HTTP 302 from the mirror to the same file on
`www.gutenberg.org` throws `InvalidDataException` naming the redirect and its target after exactly one request, and no
request reaches `www.gutenberg.org`. With the real fetcher over a fake handler that redirects the text of one book,
`Fetch` fails that book with a redirect problem, pins the other two, requests the redirected text exactly once (six
requests in all, every one to the mirror) and never pauses for the 5 s retry.

##### Verified bodies

**Tests**: `CorpusCache_Fetch_PinnedAndCached_MakesNoRequest`,
`CorpusCache_TamperedBody_RefusedThenRestored`, `CorpusCache_Fetch_MirrorTextDiffersFromPin_Fails`,
`CorpusCache_ReadBodies_UnpinnedBook_Throws`

After pinning, a second fetch reports every book cached with no request. A body with appended text is refused by
`ReadBodies`, and the next fetch restores it from the raw file without a request. A pin that no download matches makes
the book fail with a message about the pin and no body file. Reading unpinned bodies throws and says to write the pins.

##### Cancellation

**Tests**: `CorpusCache_Fetch_CallerCanceledDuringRequest_PropagatesWithoutRetry`,
`CorpusCache_Fetch_CallerCanceledDuringBodyRead_StopsWithoutWritingFile`,
`CorpusCache_Fetch_CallerCanceledDuringBackoff_StopsBeforeNextRequest`,
`CorpusCache_Fetch_DefaultPauseCanceled_EndsAtOnce`,
`CorpusCache_Fetch_TimeoutWithoutCallerCancellation_Retries`

A fetcher that cancels the caller's token and throws `TaskCanceledException` makes `Fetch` throw
`OperationCanceledException` after one call with no recorded pause. A response body that sends one byte, then cancels
the caller and stalls in its next read until that read is canceled (its non-cancelable `Read` throws), makes `Fetch`
throw `OperationCanceledException` after one call, with no pause and no body file. A fetcher that always fails with HTTP
503 and a sleeper that cancels the token make `Fetch` throw `OperationCanceledException` after one call and the single
pause of 5 s. With no injected sleeper and the token canceled 50 ms after a failed request (by a dedicated thread, so a
busy thread pool cannot delay the cancellation), `Fetch` throws `OperationCanceledException` after one call in less than
the 5-second backoff. A `TaskCanceledException` on the first call with an uncanceled token gives the pauses 5 s, 2 s and
2 s, three calls and a pinned book.
