### HeteronymClassifier

**Purpose**: Choose which reading of an inventory form a word occurrence has, by scoring only the candidate readings
of that form with the sparse model, and report a temperature-calibrated confidence for the choice; and supply the
classifier lazily and once, as a value that says what happened, through the `HeteronymModelProvider`. The unit decides
nothing about annotations, thresholds or overrides: it returns the index of the chosen reading and a number from 0 to
1.

**Data Model**:

- `HeteronymPrediction` (an internal read-only record struct): `ReadingIndex` (the table index of the chosen reading in
  the inventory) and `Confidence` (a number from 0 to 1)
- `HeteronymClassifier` (internal and sealed): the inventory and the model it was built from, an optional observer
  and the count of model-scored classifications. It is immutable apart from that counter, which is advanced with
  `Interlocked`
- `ModelLoadStatus` (an internal enumeration): `NotShipped` (no model exists: none is embedded, or the source supplied
  none; not an error), `Loaded`, `Invalid` (a model exists but cannot be read: corrupt, truncated or otherwise invalid,
  or the source failed to open) and `Incompatible` (the file is well-formed but does not fit this build)
- `HeteronymModelProvider` (internal and sealed): an optional function that opens the model, an optional inventory (the
  default inventory when null), two optional test observers (a load observer and the classification observer handed to
  the classifier) and a `Lazy<T>` in `ExecutionAndPublication` mode that holds the cached result, a classifier or a
  failure status. The static `Embedded` provider looks for the manifest resource
  `DemaConsulting.Speech.Text.En.Resources.heteronyms.bin`; this release embeds none, so `Embedded` is not configured.
  The static `None` provider has no source

**Scoring and decision.** For a form with several candidates, `Classify` extracts the hashed features of the target
once, then scores every candidate with integers only:

- the score of a candidate is the sum, over every conjoined feature hash `h`, of the model weight of the bucket
  `Mix(h, labelSalt) & mask`, plus the sum, over every shared feature hash, of the weight of the bucket
  `Mix(h, roleSalt) & mask`, where the salts are the candidate's (see *ReadingInventory Design*) and the mask keeps the
  model's hash bits. A bucket with no stored weight adds 0. The sum is an `int`; the most it can reach, about 115
  features times 32,767, fits with room to spare
- the highest score wins and a candidate replaces the current best only when its score is **strictly greater**, so an
  exact tie goes to the candidate that comes **first in the order of the rows of `forms.tsv`** (decision: the inventory
  has no default reading, and a trained model's bias feature normally breaks real ties, so exact ties are rare)
- only the candidates of the form are scored, so a reading that is not a candidate can never win, however large the
  weights of its own buckets

The decision uses integers only, so it is identical on every platform and runtime. A form with exactly one candidate is
not ambiguous: the result is that reading with confidence 1 and neither the extractor nor the model is touched.

**Confidence.** With the candidate scores `s`, the top score `m`, and the model's `scale` and `temperature`, the
confidence is `1 / sum(exp((s - m) / (scale * temperature)))` over the candidates, computed in `double` and clamped to
the range 0 to 1; it is the softmax probability of the winning candidate at the model's temperature. With no weights at
all, two candidates give exactly 0.5 and three give one third. A higher temperature lowers the confidence and never
changes the decision. Only this reported number uses `Math.Exp`; it may differ in its last bits between platforms, which
rounding to three decimals (as the analyzer does when it writes the confidence) hides except exactly on a rounding or
threshold boundary. That small boundary effect is documented and accepted; the model bytes and the decisions never
depend on it.

**Key Methods**:

- **HeteronymClassifier(inventory, model, classified)**: checks that the model fits this build and the inventory, and
  throws `InvalidDataException` otherwise: the model's feature-set version must equal the version this build extracts,
  and the model's inventory version must not exceed the inventory's (**inventory-version policy**: an older model works
  with a newer inventory because its weights bind to reading ids through salts, but a model of a newer inventory may
  score readings this build does not know). A null inventory or model throws `ArgumentNullException`. The optional
  observer receives the running count right after each model-scored classification, so a test can act at an exact
  classification (for example, cancel the analysis at an exact word); production passes null
- **Classify(sentence, target, formIndex)**: as above; the feature buffers (`MaxConjoined` and `MaxShared` entries) and
  the candidate scores are on the stack, so a classification allocates nothing. A target outside the sentence throws
  `ArgumentOutOfRangeException`. Thread-safe: any number of threads may classify concurrently on one instance and get
  the single-threaded results
- **HeteronymModelProvider.IsConfigured**: whether a model source exists, answered without opening or loading it (for
  the embedded provider, whether the manifest resource exists). An analyzer uses it at construction to decide whether
  the reading family can be effective at all
- **HeteronymModelProvider.Inventory**: the supplied inventory or the default one
- **HeteronymModelProvider.GetClassifier(out status)**: returns the shared classifier and `Loaded`, or null and the
  failure status. The first call runs the load once, under the lazy's lock; every later call, from any thread, gets the
  same result. The load opens the source (null or no source means `NotShipped`), loads the stream strictly (any
  `InvalidDataException`, `IOException`, `UnauthorizedAccessException` or `NotSupportedException` means `Invalid`, and
  so does a failure to open) and builds the classifier (an `InvalidDataException` of the compatibility check means
  `Incompatible`). The stream is disposed. **A failure is cached as a status value, not as a cached exception**, so a
  corrupt model is opened once, not on every request, and every caller sees the same status. Laziness: a provider that
  is never asked for a classifier never opens its source, however it is otherwise used
- **HeteronymModelProvider(open, inventory, loadObserver, classificationObserver)**: the constructor for a model source
  that tests and a later increment supply; the load observer is called when the load starts, which lets a test count
  loads

**Error Handling**: the constructor and `Classify` throw the argument exceptions above; the incompatibility of a model
is an `InvalidDataException` from the constructor that the provider turns into the status `Incompatible`. The provider
never throws for a model problem: absence is `NotShipped`, a damaged or unreadable model is `Invalid` and a model that
does not fit is `Incompatible`. The one exception is a defective embedded inventory table, which is a broken build with
nothing to degrade to; its `InvalidDataException` propagates from the load and, because the lazy caches an exception, is
the same for every caller. The analyzer turns `Invalid` and `Incompatible` into one fixed warning (see
*AnalysisDiagnostics Design*) and `NotShipped` into silence.

**Dependencies**: `ReadingInventory` (candidates, salts, version), `HeteronymFeatureExtractor` and `FeatureHashing`
(features and mixing), `SparsePerceptronModel` (weights and the header values) and the framework.

**Callers**: `EnglishTextAnalyzer` (one provider per analyzer, usually the shared `Embedded` one, through its
per-request reading pass) and the tests.
