### PartOfSpeechAdapter

**Purpose**: Be the one place where the `DemaConsulting.PartOfSpeech` package is used. The adapter splits a
text into sentences and tokens and, on request, tags them, and returns plain internal values, so no
PartOfSpeech type reaches a public signature and a change to that pre-release package touches only this
unit.

**Data Model**: `PartOfSpeechAdapter` is a sealed class with a factory given at construction, a lock object,
a nullable tagger field that is null until a creation succeeds, and an optional test-only observer (called
with the index of each sentence just after it is tagged; production passes null). `AdaptedToken` is an
internal read-only record struct of `Start`, `Length`, `Tag` (a Universal Dependencies tag name, or null
when not tagged), `Confidence` and `IsKnownWord`. A sentence is the list of its `AdaptedToken`s; a text
without tokens has no sentences, and every sentence has at least one token. The static `Shared` adapter
uses the package's shared default tagger as its factory; creating the adapter does not load the tagger.

**Key Methods**:

- **PartOfSpeechAdapter(taggerFactory, sentenceTagged)**: stores the factory and the optional observer;
  a null factory throws `ArgumentNullException`
- **Analyze(text, tag, cancellationToken)**: checks the token, then splits the text with the package's
  static tokenizer, which needs no tagger and whose token offsets index the given text. For each sentence,
  in order, it checks the token again and, when `tag` is false, returns the tokens untagged, or, when
  `tag` is true, obtains the tagger (the first time only), checks the token again (initialization is
  the expensive step and cannot itself be interrupted), tags the sentence, notifies the observer, checks
  the token once more and returns each token with its tag name, confidence and known-word flag. While
  tokens are copied the token is checked every 256 tokens, and once more after the last sentence, so a
  canceled request never returns a result. Tagging a tokenized sentence on its own gives the same result
  as tagging the whole text, which is why the adapter can check cancellation between and after sentences
- **GetTagger()** (private): double-checked creation. A tagger that already exists is read without
  locking; otherwise the factory runs under the lock, a null return throws `InvalidOperationException`, and
  a created tagger is published with a volatile write
- **DescribeSentence(analysisText, sentence, buffer)** (internal static): describes every token of one adapted sentence
  for the heteronym classifier, reusing a buffer. It makes one `ContextToken` per token with
  `HeteronymContext.Create(analysisText, start, length, tag, confidence)` and returns a buffer whose first
  `sentence.Count` entries describe the sentence; a null buffer, or one shorter than the sentence, is replaced by a new
  array, so the caller's array grows to the longest sentence it describes. It is the **single place where an adapted
  token becomes a context token**: the analyzer's per-request reading pass calls it at run time and the offline trainer
  calls it when it prepares a row, so the two cannot describe a sentence differently. It lives in the adapter, with the
  adapted token it consumes, rather than in the analyzer, so that the HeteronymSubsystem does not depend on the
  AnalysisSubsystem (it was a private method of the analyzer's reading pass before the trainer needed it; the behavior
  is unchanged). It is stateless and thread-safe as long as the caller does not share the buffer, uses no tagger and
  does not touch the cancellation token
- **IsTaggerCreated**: whether a tagger has been created yet; used by tests

**Tagger creation pattern (design decision)**: the tagger is created under a plain lock with a null check
instead of `Lazy<T>`. A `Lazy<T>` in `ExecutionAndPublication` mode (the only mode that never runs the
factory twice) caches an exception thrown by its factory and rethrows it for the lifetime of the
`Lazy<T>`, here the whole process for the `Shared` adapter, so one transient failure while inflating the
tagger's data (for example an out-of-memory condition) would disable English analysis until restart.
`LazyThreadSafetyMode.PublicationOnly` does retry after an exception, but it lets several threads run the
factory at once and discards all but one result; construction inflates about 1.3 MB of data and is
expensive and may race, so duplicating it is unsuitable. The lock-and-null-check pattern keeps all the
required properties: not loaded until first needed, created at most once on success, thread-safe, retry
after a failure, and an injectable factory for tests. The cost is that callers racing a failing first
attempt each see the factory run in turn (serialized by the lock) until one succeeds; that is acceptable
because a failure is exceptional.

**Error Handling**: a null text or factory throws `ArgumentNullException`; a canceled token throws
`OperationCanceledException` at whichever check observes it. An exception from the tagger factory
propagates to the caller and is not remembered: the next request that needs the tagger invokes the
factory again. A factory that returns null throws `InvalidOperationException`. `DescribeSentence` throws what
`HeteronymContext.Create` throws (`ArgumentNullException` for a null text, `ArgumentOutOfRangeException` for a token
span outside the text), which cannot happen for tokens the adapter itself returned for that text.

**Dependencies**: OTS item `DemaConsulting.PartOfSpeech` (see *PartOfSpeech Design*): the tokenizer
(`Tokenizer.Tokenize`), the tagger (`Tagger.Default`, `Tagger.Tag`), the token and tagged-token types, and the tag-name
conversion; and, for `DescribeSentence` only, the HeteronymSubsystem's `HeteronymContext` and `ContextToken`.

**Callers**: `EnglishTextAnalyzer` (`Analyze`, and `DescribeSentence` from its reading pass); the trainer's row
preparation (`Analyze` and `DescribeSentence`); the unit tests.
