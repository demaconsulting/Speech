## HeteronymSubsystem Design

![HeteronymSubsystem Structure](TextEnHeteronymSubsystemView.svg)

### Overview

The SpeechTextEn HeteronymSubsystem chooses which pronunciation reading a written word has when the word is a
heteronym, such as the noun and the verb of `record` or the present and the past of `read`. It owns the inventory of
readings, the deterministic features that describe a word in its sentence, the format and the strict loader of the
trained weights, and the classifier that scores the candidate readings of one word and reports a calibrated
confidence. It knows nothing of annotations, sentence breaks, overrides or diagnostics: it answers one question,
"which of the candidate readings of this word is the most likely one in this sentence, and how sure is that", and the
AnalysisSubsystem turns the answer into annotations. It contains the following units:

- **ReadingInventory**: the public, immutable inventory of readings and of the written forms that can carry them,
  loaded from the two embedded tables `readings.tsv` and `forms.tsv`, with the shared grammar of a reading id
- **HeteronymFeatureExtractor**: the internal feature set (version 1): the description of one token
  (`HeteronymContext`, `ContextToken`), the hash functions (`FeatureHashing`) and the extraction of the hashed
  features of a target word from the tokens of its sentence
- **SparsePerceptronModel**: the internal sparse averaged-perceptron model: the `HETW` file format, its strict loader
  (with the internal writer that tests and the later trainer use), and the immutable weight lookup
- **HeteronymClassifier**: the internal candidate-restricted classifier, with the `HeteronymModelProvider` that
  supplies it lazily and once

The model itself is data, not code: this release embeds the two inventory tables but no trained weights, so the
classifier is effective only where a model source exists (see *Heteronym Readings* in *SpeechTextEn System Design*).

### Interfaces

The subsystem exposes one public type, `ReadingInventory`, in the `DemaConsulting.Speech.Text.En.HeteronymSubsystem`
namespace: a host uses it to check a reading id, to list the readings of the inventory and to list the readings a
written form can carry. Everything else is internal: `HeteronymContext`, `ContextToken`, `HeteronymFeatureExtractor`
and `FeatureHashing` (the features), `SparsePerceptronModel`, `SparsePerceptronModelWriter` and `ModelBinaryFormat`
(the model), and `HeteronymClassifier`, `HeteronymPrediction`, `HeteronymModelProvider` and `ModelLoadStatus` (the
decision). The internal `ReadingIdSyntax` is the one grammar of a reading id, shared with the AnalysisSubsystem's
reading override validation and typed annotation reader.

The subsystem consumes only the framework: it names no PartOfSpeech type, no Speech type and no AnalysisSubsystem
type. The dependency between the subsystems is one way: the AnalysisSubsystem uses the HeteronymSubsystem, never the
reverse. The analyzer describes a token by calling `HeteronymContext.Create` with the token's offsets in the text,
the tag name and the tag confidence, which are plain values, so the tagger's types never cross the boundary. The
tests of this subsystem reach the tagger only through the test fixture that builds contexts like the analyzer does
(see *HeteronymSubsystem Verification*).

### Design

**Question and answer.** For a word that is a form of the inventory, the analyzer asks the classifier for a reading
with the tokens of the word's sentence, the index of the word in it and the index of its form. The classifier looks up
the candidates of the form. A form with one candidate is not ambiguous and is answered with confidence 1 without
touching the model. For a form with several candidates the extractor produces the hashed features of the word in its
sentence once, the classifier scores each candidate by adding the model weights its features select through that
candidate's salts, picks the highest integer score (a tie goes to the first candidate in the order of `forms.tsv`) and
converts the scores to a confidence with the model's temperature. The result is the reading's index in the inventory and
a confidence from 0 to 1.

**Unit collaboration.** `ReadingInventory` is the only unit that knows the data tables: it answers whether a token is
a form, which candidates it has, and the label and role salt of each reading, which it computes once with
`FeatureHashing`. `HeteronymFeatureExtractor` is the only unit that knows the templates; it depends on nothing else
of the subsystem except the hash functions and the token description. `SparsePerceptronModel` is the only unit that
knows the bytes of a model file and the only one that decodes them; it knows nothing of readings, only of buckets and
weights. `HeteronymClassifier` is the only unit that combines the three: it binds a feature hash to a reading with the
reading's salt, masks it to the model's hash bits and reads the weight. `HeteronymModelProvider` is the only unit that
opens a model and decides whether it is usable, and it reports the result as a status value, never as an exception.

**Feature parity (decision).** A trained model is valid only while the features the runtime extracts are exactly the
features the trainer extracted. The two must therefore call the same code rather than two copies of it: the feature
extractor, `HeteronymContext.Create` and `FeatureHashing` are the single implementation, and the later trainer will
reach them through `InternalsVisibleTo` granted to the trainer assembly when the trainer is added. The alternatives were
rejected for stated reasons: a source-linked copy compiled into the trainer drifts and doubles the review, and a public
extractor freezes the feature set under semantic versioning although the feature set is meant to change with a
feature-set version. No `InternalsVisibleTo` is added now, because it would be dead configuration until the trainer
exists. Two guards keep the parity checkable: golden feature vectors, which lock the exact hashes of a set of
sentences under several cultures, and a tokenization-parity test, which proves that equivalent spellings that the
tokenizer splits differently (`can't` and `ca n't`, `John's` and `John 's`, quotes, brackets, abbreviations) give the
same token texts and the same features for the target. A feature-set version in every model file makes a mismatch an
incompatible model instead of silent wrong weights (see *HeteronymFeatureExtractor Design*).

**Inventory-version policy (decision).** The inventory carries an integer revision that is a constant of the code; the
revision grows when readings are added. A model records the revision it was trained for and binds its weights to
reading ids, through salts derived from the id text, never to table positions. A model is therefore accepted when its
revision is the same as or lower than the inventory's revision (a newer inventory only adds readings the model never
scored) and rejected when it is higher (the model may score readings this build does not know). The check is made by
the classifier, because it needs both the model and the inventory (see *HeteronymClassifier Design*).

**Embedded data and the missing model.** The inventory tables are the repository's data files, embedded by link under
fixed manifest names, so the data has one source of truth. The model is looked for under one more fixed manifest
name, and this release embeds none, so `HeteronymModelProvider.Embedded` is not configured: the analyzer sees that
without loading anything and emits no classified readings. A later increment adds the file and nothing else needs to
change in this subsystem.

**Concurrency.** Every unit is immutable after construction or stateless. The default inventory and each provider's
result are created under `Lazy<T>` with `ExecutionAndPublication`, so concurrent first use loads each exactly once. The
provider caches a failure as a status value and never as a cached exception, so a corrupt model is opened once and
reported consistently. A defective embedded inventory table is a broken build with nothing to degrade to; it throws
`InvalidDataException` and is not retried. The classifier and the model are read-only after loading, so one classifier
serves any number of concurrent requests; the feature buffers of a classification are on the stack, so a
classification allocates nothing.

**Cancellation.** Nothing in this subsystem checks a cancellation token: a classification is a few hundred integer
additions, and a model load is a single bounded operation. The analyzer checks its token before each candidate word and
so never starts a classification for a canceled request.
