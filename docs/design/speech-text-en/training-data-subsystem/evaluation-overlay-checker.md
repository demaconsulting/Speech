### EvaluationOverlayChecker

**Purpose**: Keep the two evaluation overlays of the data folder what they claim to be. The regression overlay
(`regression.tsv`) refers to owner-flagged sentences, golden test sentences, analyzer test sentences and existing
training or development rows, and says which of them the model trained on; it must never touch a holdout. The
generalization set (`generalization.tsv`) is a test set written blind; its rows must stay new against the training and
development data as that data changes, and the written set must be balanced enough for the pre-registered comparison.
The unit also checks a staged copy of the generalization set before it is committed, with reasons that reveal no
training data to the blind writer.

**Data Model**: `EvaluationOverlayChecker` holds the loaded `TextEnDataSet`, the `CheckReport`, a `GeneralizationShape`
and two maps built once from the inventory: the *lemma group* of every lemma and the surface forms of every group. Its
findings use the check name `overlay`.

- **Lemma group**: the lemmas whose readings share a surface form in `forms.tsv` form one group (union-find over the
  forms), named by its ordinally smallest lemma. `wound` is a form of both `wound.injury` and `wind.coil`, so `wind` and
  `wound` are one group; *lives* joins `life` and `live`. Novelty is judged per group, so a construction of *wind* in
  training counts against a row of *wound* that has the same target form.
- **Indexes** (built on first use, each item mapped to the first row that has it): the sentence keys of all four data
  files, of the training rows and of the holdout rows; the word 5-grams of the training rows, of the training and
  development rows and of the holdout rows; per lemma group, the words of every training and development sentence and
  the construction trigrams around the target of every training and development row; the sentence keys of the
  regression overlay. Keys, words and 5-grams are those of `SentenceKeys`.
- **Near duplicate**: a sentence of at least 8 words whose word 5-grams are at least 80 percent contained in an index,
  the thresholds of `LeakageChecker`.
- **Construction trigram**: the words at the positions -2..0, -1..+1 and 0..+2 around the target, with the target
  replaced by `<T>` and prefixed `L|`, `C|` and `R|`; a trigram that would extend beyond the sentence is not formed, so
  a sentence-initial target is not novel or familiar by its position alone.
- **GeneralizationShape**: a read-only record of the quotas; `Default` is 30 families, 2 to 12 rows per family, 90
  `novel-vocabulary` rows, 70 `novel-construction` rows, 60 `cue_unsolvable=yes` rows over 8 lemmas, and 2 readings per
  lemma group, the values fixed in the protocol of the run log. Tests pass smaller quotas.
- **Regression origins and references**: `listening` (`RUN-LOG.md:<line>` or `<Name>Tests.cs:<line>`),
  `golden-readings` (`readings-golden.tsv:<line>`), `golden-sentences` (`sentences-golden.tsv:s<n>`), `golden-pipeline`
  (`piper-pipeline-golden.tsv:<id>` or `kokoro-pipeline-golden.tsv:<id>`), `analyzer-test` (`<Name>Tests.cs:<line>`)
  and `data-row` (`train.tsv:<id>` or `dev.tsv:<id>`).

**Key Methods**:

- **EvaluationOverlayChecker(data, report, shape)**: stores the arguments (a null shape means the default) and builds
  the lemma groups; a null data set or report throws `ArgumentNullException`.
- **Run()**: `CheckRegression()`, then `CheckGeneralization` on the committed set with matches revealed.
- **CheckRegression()**: for every row, in file order: the identifier (`reg-NNNN`, unique); no second row on the same
  sentence and start offset; the reading, the lemma and the strict span (through `SpanValidator`); the license
  (allow-list of `LicenseIds`); the reference (a reference into `holdout-natural.tsv` or `holdout-independent.tsv` is a
  finding whatever the origin; otherwise the origin must be known and the reference must fit its grammar; a `data-row`
  reference must resolve to a row of that file with the same lemma, reading, sentence, offsets and license); the holdout
  separation (the key may not be a holdout key and the sentence may not be a near duplicate of a holdout sentence); and
  the seen mark, recomputed as `yes` exactly when the row refers to a training row, its key is a training key or it is a
  near duplicate of a training sentence. A row of a test-only origin (`listening`, `analyzer-test`, `golden-pipeline`,
  `golden-sentences`) whose recomputed mark is `yes` is a finding on the `leakage` check, whatever its mark says: test
  sentences are never training rows. `golden-readings` is left out, because some of its legacy rows repeat training
  sentences; the recomputed mark protects it. It adds an information line with the rows per origin and the seen and
  unseen counts. Golden and test references are not opened here: the data check does not know the repository layout, so
  a trainer test resolves them from the repository's test files.
- **CheckGeneralization(rows, revealMatches)**: for every row: the identifier (`gen-NNNN`, unique) and no repeated
  sentence key within the set; the reading, lemma and span; the columns (kind `novel-vocabulary` or
  `novel-construction`; family `{lemma}:{cue-type}` in lower-case ASCII whose lemma is in the row's lemma group;
  `cue_unsolvable` `yes` or `no`; source `generalization`; license `CC0-1.0`); 6 to 30 words; at most one word that is a
  form of the row's lemma group; cue forms (`-` only on a construction row, otherwise single lower-case words of the
  sentence that are not a form of the group). Then the novelty rules:
  - **N1**: the key is not a key of the four data files or of the regression overlay, and the sentence is not a near
    duplicate of a training or development sentence;
  - **N2** (`novel-vocabulary`): no cue form is a word of a training or development sentence of the group;
  - **N3** (`novel-construction`, valid span): no construction trigram of the row is a construction trigram of a
    training or development row of the group.

  With `revealMatches` the N1 to N3 findings name the matching row (`(train.tsv syn-00001)`, or the trigram); without
  it they carry the fixed reasons `GenericN1`, `GenericN2` (after the writer's own cue form) and `GenericN3` only. With
  no rows it adds the information line that the quotas apply once the set is written; otherwise it applies the shape
  quotas (each lemma group's readings, the number of families and the rows of each family, the rows of each kind, the
  cue-unsolvable rows and their lemmas, each shortfall one finding on the file) and adds a summary information line.
- **GroupOf(lemma)**: the group of a lemma; the lemma itself when the inventory does not know it.

**Error Handling**: every violation is a finding; the methods never throw for the content of the data. A row whose
reading or span is invalid is still checked for everything else, but N3 needs a valid span and skips the row otherwise.

**Dependencies**: `TextEnDataSet` (with `RegressionRecord`, `GeneralizationRecord`, `DataRow` and `LicenseIds`),
`SentenceKeys`, `SpanValidator` and `CheckReport`; regular expressions of the base class library.

**Callers**: `CheckDataCommand` and the unit tests.
