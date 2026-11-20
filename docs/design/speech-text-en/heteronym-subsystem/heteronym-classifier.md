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
- `HeteronymClassifier.IWeightSource` (an internal nested interface): a source of integer bucket weights with one
  method, `GetWeight(bucket)`, returning 0 for a bucket without a weight. `ModelWeights` (a private read-only record
  struct) is the loaded `SparsePerceptronModel` as a weight source; the trainer supplies its own structs for its live
  and deployable weights. Both are structs so the generic scoring call is specialized and allocation-free
- `ModelLoadStatus` (an internal enumeration): `NotShipped` (the provider has no model source, or the source supplied
  none; not an error), `Loaded`, `Invalid` (a model exists but cannot be read: corrupt, truncated or otherwise invalid,
  or the source failed to open) and `Incompatible` (the file is well-formed but does not fit this build)
- `HeteronymModelProvider` (internal and sealed): an optional function that opens the model, an optional inventory (the
  default inventory when null), two optional test observers (a load observer and the classification observer handed to
  the classifier) and a `Lazy<T>` in `ExecutionAndPublication` mode that holds the cached result, a classifier or a
  failure status. The static `Embedded` provider looks for the manifest resource
  `DemaConsulting.Speech.Text.En.Resources.heteronyms.bin`, which the package embeds, so `Embedded` is configured and
  loads the shipped model. The static `None` provider has no source, so it is not configured and its status is
  `NotShipped`

**Scoring and decision.** For a form with several candidates, `Classify` extracts the hashed features of the target
once, then scores every candidate with integers only:

- the score of a candidate is the sum, over every conjoined feature hash `h`, of the model weight of the bucket
  `Bucket(h, labelSalt, mask)`, that is `Mix(h, labelSalt) & mask`, plus the sum, over every shared feature hash, of the
  weight of the bucket `Bucket(h, roleSalt, mask)`, where the salts are the candidate's (see *ReadingInventory Design*)
  and the mask keeps the model's hash bits. A bucket with no stored weight adds 0. The sum is an `int`; the most it can
  reach, about 115 features times 32,767, fits with room to spare. A feature hash that occurs twice counts twice
- the highest score wins and a candidate replaces the current best only when its score is **strictly greater**, so an
  exact tie goes to the candidate that comes **first in the order of the rows of `forms.tsv`** (decision: the inventory
  has no default reading, and a trained model's bias feature normally breaks real ties, so exact ties are rare)
- only the candidates of the form are scored, so a reading that is not a candidate can never win, however large the
  weights of its own buckets

The decision uses integers only, so it is identical on every platform and runtime. A form with exactly one candidate is
not ambiguous: the result is that reading with confidence 1 and neither the extractor nor the model is touched.

**Adjective cue.** Grammar that marks the word as an adjective is added to the model's evidence as a fixed prior. When
the tagger tags the target `ADJ` and either a degree adverb tagged `ADV` stands right before it (`very`, `so`, `too`,
`quite`, `extremely`, `most` and 23 more: *very minute*, *so close*, *quite content*) or a coordinating conjunction
joins it to a word tagged `ADJ` (*minute and clear*, *grey and minute*, also after one degree adverb on the right:
*minute and very clear*), every candidate whose part-of-speech hint includes `ADJ` (see *ReadingInventory Design*,
`IsAdjective`) gets the integer bonus `round(scale * temperature * 2.2)` added to its score: odds of about 9 to 1 on the
model's own calibrated scale (`AdjectiveCueLogOdds`). Both signals must agree, the tag and the construction, so a degree
word in another role (*the very minute he came*, where *very* is tagged `ADJ` and *minute* `NOUN`) does not trigger it.
It is a prior, not a rule: a model that is confident of another reading keeps it. Decision: the sparse features see
these constructions too rarely to weigh them (the listener-flagged *grey and very minute and clear* got the time reading
at 0.59), while a degree adverb before a word that the tagger reads as an adjective leaves almost no room for a noun or
a verb. The bonus is computed once from the model's header, so the decision stays integer-only, and the trainer never
sees it: the model and its training record are unchanged, and the evaluation (which runs the analyzer and the
classifier) measures it. The measured effect is in `tools/DemaConsulting.Speech.Text.En.Trainer/RUN-LOG.md`.

**Noun-form cue.** A reading can be a noun of its lemma and never a noun of a given form: `wind.coil` is a noun as *a
wind* (a bend), but *wound* is only its past tense (see *ReadingInventory Design*, `CanBeNoun`). When the tagger tags
the target `NOUN`, exactly one candidate can be a noun of the form, and another candidate is ruled out by the form
itself, that candidate gets `round(scale * temperature * 1.0)` (`NounFormCueLogOdds`, odds of about 2.7 to 1). Decision:
the flagged *a knife wound* got the coil reading at 0.55. The prior is small on purpose: the tagger tags a verb `NOUN`
now and then (*the tape wound onto the spool*), and a prior of ln 9 or more, or a hard constraint, lost that development
row; a cue for every noun or verb tag over all heteronyms was measured too and lost 1 to 2 points of development
accuracy and 11 rows of the external `whd-eval` set, so the cue is limited to the forms that rule a reading out by
themselves.

**Past-time cue.** When the sentence has *yesterday*, *ago*, *previously* or *last* before a word of time (*last
night*, *last week*) and the target does not follow an auxiliary or a particle (*did not read*, *to read*, *will read*
take the base form whatever the time; one adverb may stand between), every past-tense candidate (role `past`, see
`IsPastTense`) gets `round(scale * temperature * 2.2)` (`PastTimeCueLogOdds`). Decision: the tense of *read* depends on
words the sparse features often do not see together with it.

**Context cues.** A short table of cue words per reading marks a use the training data seldom shows: for `tear.rip`,
words of the body and of materials (*muscle*, *ligament*, *tendon*, *hamstring*, *fabric*, *canvas*, *seam*, *wear*
and 15 more). A candidate with a cue word within three words of the target (`ContextCueWindow`) gets
`round(scale * temperature * 2.2)` (`ContextCueLogOdds`). Decision: the training data has one noun use of the rip
reading against its verb uses and the noun uses of the drop from the eye, so the flagged *a deep muscle tear* got the
drop at 0.72. Adding training rows was tried (a fourth synthetic round of 110 rows): it fixed the flagged words, but
retraining the single-epoch model re-rolled other readings (*the bass player caught a bass*, *a tear in his eye*, *I
read every night*), so the data round was not kept. All three cues, like the adjective cue, are priors that the trainer
never sees and that leave a confident model its reading.

**Confidence.** With the candidate scores `s`, the top score `m`, and the model's `scale` and `temperature`, the
confidence is `1 / sum(exp((s - m) / (scale * temperature)))` over the candidates, computed in `double` and clamped to
the range 0 to 1; it is the softmax probability of the winning candidate at the model's temperature. With no weights at
all, two candidates give exactly 0.5 and three give one third. A higher temperature lowers the confidence and never
changes the decision. Only this reported number uses `Math.Exp`; it may differ in its last bits between platforms, which
rounding to three decimals (as the analyzer does when it writes the confidence) hides except exactly on a rounding or
threshold boundary. That small boundary effect is documented and accepted; the model bytes and the decisions never
depend on it.

**Key Methods**:

- **`ScoreCandidate<TWeights>`(weights, conjoined, shared, labelSalt, roleSalt, mask)** (internal static): the integer
  score of one candidate reading as defined above: the sum of the weights of the buckets the conjoined hashes select
  through the label salt and the shared hashes select through the role salt (`FeatureHashing.Bucket`), one term per
  occurrence. It is the **single definition of a candidate's score**: `Classify` scores a loaded model with it, and the
  offline trainer scores its training and deployable weights with it, so training and run time cannot disagree about a
  score, a decision or a tie. It reads only the spans and the weight source it is given and allocates nothing

- **HeteronymClassifier(inventory, model, classified)**: checks that the model fits this build and the inventory, and
  throws `InvalidDataException` otherwise: the model's feature-set version must equal the version this build extracts,
  and the model's inventory version must not exceed the inventory's (**inventory-version policy**: an older model works
  with a newer inventory because its weights bind to reading ids through salts, but a model of a newer inventory may
  score readings this build does not know). A null inventory or model throws `ArgumentNullException`. The optional
  observer receives the running count right after each model-scored classification, so a test can act at an exact
  classification (for example, cancel the analysis at an exact word); production passes null
- **Classify(sentence, target, formIndex)**: as above, scoring every candidate with `ScoreCandidate` over a
  `ModelWeights` of the loaded model; the feature buffers (`MaxConjoined` and `MaxShared` entries) and
  the candidate scores are on the stack, so a classification allocates nothing. A target that is negative or not below
  the sentence length throws `ArgumentOutOfRangeException`; it is checked first, before the single-candidate shortcut,
  so every form rejects a bad target. Thread-safe: any number of threads may classify concurrently on one instance and
  get the single-threaded results
- **HeteronymModelProvider.IsConfigured**: whether a model source exists, answered without opening or loading it (for
  the embedded provider, whether the manifest resource exists). An analyzer uses it at construction to decide whether
  the reading family can be effective at all
- **HeteronymModelProvider.Inventory**: the supplied inventory or the default one
- **HeteronymModelProvider.GetClassifier(out status)**: returns the shared classifier and `Loaded`, or null and the
  failure status. The first call runs the load once, under the lazy's lock; every later call, from any thread, gets the
  same result. The load opens the source (null or no source means `NotShipped`), loads the stream strictly (any
  `InvalidDataException`, `IOException`, `UnauthorizedAccessException` or `NotSupportedException` means `Invalid`, and
  so does a failure to open; a well-formed model of another feature-set version loads) and builds the classifier (an
  `InvalidDataException` of the compatibility check, a feature-set mismatch or a newer inventory, means
  `Incompatible`). The stream is disposed. **A failure is cached as a status value, not as a cached exception**, so a
  corrupt model is opened once, not on every request, and every caller sees the same status. Laziness: a provider that
  is never asked for a classifier never opens its source, however it is otherwise used
- **HeteronymModelProvider(open, inventory, loadObserver, classificationObserver)**: the constructor for a model source
  that tests supply (and that `Embedded` uses for the resource); the load observer is called when the load starts, which
  lets a test count loads

**Error Handling**: the constructor and `Classify` throw the argument exceptions above; the incompatibility of a model
is an `InvalidDataException` from the constructor that the provider turns into the status `Incompatible`. The provider
never throws for a model problem: absence (no model source) is `NotShipped`, a damaged or unreadable model is `Invalid`
and a model that does not fit is `Incompatible`. The one exception is a defective embedded inventory table, which is a
broken build with nothing to degrade to; its `InvalidDataException` propagates from the load and, because the lazy
caches an exception, is the same for every caller. The analyzer turns `Invalid` and `Incompatible` into one fixed
warning (see *AnalysisDiagnostics Design*) and `NotShipped` (no model source) into silence.

**Dependencies**: `ReadingInventory` (candidates, salts, version), `HeteronymFeatureExtractor` and `FeatureHashing`
(features and mixing), `SparsePerceptronModel` (weights and the header values) and the framework.

**Callers**: `EnglishTextAnalyzer` (one provider per analyzer, usually the shared `Embedded` one, through its
per-request reading pass), the trainer (`ScoreCandidate` and `IWeightSource` for training and model selection, and
`Classify` for the evaluator's direct path) and the tests.
