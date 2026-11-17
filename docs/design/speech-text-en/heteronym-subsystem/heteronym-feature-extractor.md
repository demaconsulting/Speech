### HeteronymFeatureExtractor

**Purpose**: Turn one tagged sentence and a target word into the hashed features that the heteronym model scores,
deterministically, culture-independently and without allocating. The unit is the single definition of the feature
set: the runtime and the later trainer must extract exactly the same features, so both call this code (see *Feature
parity* in *HeteronymSubsystem Design*). It comprises `HeteronymContext` (the description of one token),
`FeatureHashing` (the hash functions and the salts) and `HeteronymFeatureExtractor` (the templates).

**Data Model**:

*Feature-set version.* `FeatureSetVersion` is 1. Every model file records it and the loader rejects any other value,
so a change of a template, a template number, a word list, a code or a hash must raise the version, which makes every
older model impossible to load instead of silently wrong.

*The token description* (`ContextToken`, an internal read-only record struct): `Start` and `Length` (the token's span in
the analyzed text), `Upos` (the part-of-speech code), `ConfidenceBucket`, `Class`, `Shape`, `IsWord`, `LowerHash` and
`LowerLength` (the hash and the length of the normalized lower-case form), `Suffix1Hash`, `Suffix2Hash` and
`Suffix3Hash` (the hashes of the last one, two and three code units of the normalized form; 0 when the form is
shorter), and `SurfaceHash` (the hash of the exact text, only for punctuation; otherwise 0). All of it is computed once
by `HeteronymContext.Create(text, start, length, tag, confidence)`, so extraction never touches the text again and the
trainer and the analyzer cannot describe a token differently. Every test of a character is made per Unicode scalar with
culture-free `Rune` tests (a lone surrogate counts as U+FFFD, which is not a letter); the current culture is never
consulted.

- **Part of speech code**: the 17 Universal Dependencies tag names in alphabetical order are 1 (`ADJ`) to 17 (`X`); a
  null, differently cased or unknown name is 0
- **Confidence bucket**: 3 for a tag confidence of at least 0.99, 2 for at least 0.9, 1 for at least 0.7, otherwise 0
  (also for a NaN)
- **Normalized lower-case form**: the token's UTF-16 code units with ASCII capitals folded to lower case and the two
  typographic single quotes (U+2018 and U+2019) written as the plain apostrophe; every token that has a digit and no
  letter (`1999`, `$5`, an Arabic-Indic digit) has the single form `<num>`. No other character changes, so the Turkish
  dotted capital I is not an `i` and the Kelvin sign is not a `k`
- **Class**: 0 an ordinary word, 1 punctuation (no letter and no digit), 2 a number, 3 determiner, 4 pronoun, 5 modal,
  6 auxiliary, 7 `to`, 8 negation, 9 preposition, 10 conjunction, and 255 for a position outside the sentence. The
  function-word classes come from closed word lists compared with a `switch` over the span (no dictionary and no
  allocation); a normalized form longer than 12 code units is never a function word. The lists are part of the
  feature-set definition
- **Shape**: 0 outside the sentence, 1 no letter or digit, 2 only digits, 3 no capital, 4 one capital letter alone, 5
  only capitals (two or more), 6 an initial capital only, 7 mixed

*The templates.* A feature is the hash of a template number and two arguments. A **conjoined** feature (C) is scored
through the label salt of one specific reading, so its weight belongs to that reading of that word; a **both** feature
(B) is scored through the label salt and through the role salt (shared across every lemma whose reading has the same
role, such as `noun`). Positions are relative to the target (offsets). A word at an offset outside the sentence is the
marker `<S>` before it or `</S>` after it, a class there is 255, a shape 0 and a tag code 0. The table is normative:

| No. | Name | Kind | Arguments | Emitted |
| ---: | --- | --- | --- | --- |
| 0 | Bias | C | none | always |
| 1 | Target | C | target form hash | always |
| 2 | TargetSuffix | B | k, suffix hash | k = 1 to 3, when the form has k units |
| 3 | TargetShape | C | target shape | always |
| 4 | Word | C | offset, form hash | offsets -3 to +3 except 0 |
| 5 | PrevBigram | C | words at -2 and -1 | always |
| 6 | NextBigram | C | words at +1 and +2 | always |
| 7 | Surround | C | words at -1 and +1 | always |
| 8 | NeighborShape | C | offset, shape | offsets -2, -1, +1, +2 |
| 9 | NeighborSuffix2 | C | offset, suffix2 | neighbor is a word, form over 2 units |
| 10 | NeighborSuffix3 | C | offset, suffix3 | neighbor is a word, form over 3 units |
| 11 | Class | B | offset, class | offsets -3 to +3 except 0 |
| 12 | ClassPairLR | B | classes at -1 and +1 | always |
| 13 | ClassPairLL | B | classes at -2 and -1 | always |
| 14 | ClassPairRR | B | classes at +1 and +2 | always |
| 15 | FuncWord | B | offset, form hash | offsets -2, -1, +1, +2, class 3 or more |
| 16 | DetDist | B | distance, 0 | always; 1 to 3, or 0 for none |
| 17 | ModalDist | B | distance, 0 | as template 16, for modals |
| 18 | ToDist | B | distance, 0 | as template 16, for `to` |
| 19 | AuxDist | B | distance, 0 | as template 16, for auxiliaries |
| 20 | NegDist | B | distance, 0 | as template 16, for negations |
| 21 | Initial | B | 1 when no word precedes | always |
| 22 | PosIdx | B | words before, at most 3 | always |
| 23 | PunctPrev | B | punctuation feature | always |
| 24 | PunctNext | B | punctuation feature | always |
| 25 | LastWord | B | 1 when no word follows | always |
| 26 | Bag | C | form hash | each content word within 8 tokens |
| 27 | TargetUpos | B | tag code, bucket | always |
| 28 | TargetUposOnly | B | tag code | always |
| 29 | NeighborUpos | B | offset, tag code | offsets -2, -1, +1, +2 |
| 30 | UposBigram | B | tag pair, with a pair number | five pairs: -1/0, 0/+1, -1/+1, -2/-1, +1/+2 |
| 31 | PrevWordTarget | C | word at -1, target form hash | always |

**Template notes:**

- **Distance** (templates 16 to 20): the distance to the nearest token of the class among the three tokens to the left
  of the target, 1 to 3, or 0 when there is none or the sentence edge is met first
- **Punctuation feature** (templates 23 and 24): 1 at the sentence edge, 2 plus the hash of the mark for punctuation,
  otherwise 0
- **Content word** (template 26): a token that is a word, has class 0 and a form of more than two code units, at an
  offset from -8 to +8 except 0
- **Position** (templates 21, 22 and 25): a word is a token that has a letter or a digit
- **Pair number** (template 30): the first argument of the five pairs is the tag code of the first position OR-ed with
  0x100, 0x200, 0x300, 0x400 and 0x500 in the order of the table; the second argument is the tag code of the second
  position
- **Offsets** inside a hash argument are the offset plus 8, so the argument is never negative

*Bounds.* A target yields at most 78 conjoined and at most 37 shared hashes (the shared ones are the both-kind
features; every one of them is also a conjoined hash), so the caller buffers of `MaxConjoined` (96) and `MaxShared`
(48) always suffice.

*Differences from the measured prototype set* (documented because a reader may compare the two): the context is the
target's own sentence (the prototype crossed sentence boundaries, while training rows are single sentences), and no
separate homograph hash is mixed into a conjoined feature, because the label salt is derived from the full reading id,
which starts with the lemma, so the conjunction with the word is implicit and exact.

*Hashing* (`FeatureHashing`, static): no function calls `string.GetHashCode`, a culture-aware comparison or a
case-mapping API.

- **Fnv1a(text)**: the 32-bit FNV-1a hash over the UTF-16 code units (each code unit's 16 bits are XOR-ed in whole),
  from the offset basis 2166136261 and the prime 16777619. **Fnv1aFolded** folds ASCII capitals first. **FoldAscii**
  maps `A` to `Z` to `a` to `z` and nothing else
- **Mix(hash, value)**: the Murmur3 combine (the key mix, one round and the finalizer), a good spread of every input bit
- **Feature(template, first, second)**: `Mix(Mix(Mix(0x5EED, template), first), second)`
- **LabelSalt(readingId)**: the FNV-1a hash of the text `L:` followed by the id. **RoleSalt(role)**: the FNV-1a hash of
  `R:` followed by the role. Both are computed once per reading by the inventory
- A feature hash is bound to a reading by the classifier as `Mix(featureHash, salt)` masked to the model's hash bits
  (see *HeteronymClassifier Design*); the extractor itself outputs feature hashes, not buckets, so one extraction serves
  every candidate and every hash width

**Key Methods**:

- **HeteronymContext.Create(text, start, length, tag, confidence)**: validates the span without computing
  `start + length`, so extreme values cannot overflow (`start` must not exceed the text length and `length` must be
  positive and not exceed what remains after `start`; otherwise `ArgumentOutOfRangeException`), then computes every
  field of the token description in two short passes over the token. A null text throws `ArgumentNullException`. The
  suffix hashes are computed over the normalized characters, so a typographic apostrophe in a suffix is the plain one,
  and a number token has the suffixes of `<num>`
- **HeteronymFeatureExtractor.Extract(sentence, target, conjoined, shared, out conjoinedCount, out sharedCount)**:
  writes the feature hashes of the target token into two caller-supplied buffers in the fixed template order and
  reports how many it wrote. A target outside the sentence throws `ArgumentOutOfRangeException`. A buffer that is
  too small throws `InvalidOperationException`, which is a defect of the caller, never a data condition; each append is
  checked, so a small buffer is reported and never overrun. Allocation-free and thread-safe; the same input always
  writes the same hashes in the same order
- **FeatureHashing**: `Fnv1a`, `Fnv1aFolded`, `FoldAscii`, `Update`, `Mix`, `Feature`, `LabelSalt` and `RoleSalt` as
  above

**Golden vectors and the parity policy.** The file `TestData/heteronym-feature-vectors.tsv` of the test project locks
the extraction. Its header is `id`, `tokens`, `target`, `conjoined` and `shared`
(tab-separated, LF); a `tokens` cell is the sentence as space-separated `text:TAG:confidence` items, the `target` is the
index of the target token and the last two columns are the hashes in lower-case hexadecimal, comma-separated, in
emission order. The rows cover a verb and a noun use, the sentence edges, a split clitic, a typographic apostrophe,
numbers, capitalization, a non-ASCII neighbor, a supplementary-plane letter, punctuation neighbors, an untagged
token, a long sentence and the closed classes. The test reproduces every row under the invariant, `tr-TR`, `de-DE` and
`ar-SA` cultures, so no step depends on the current culture. A change of any hash is a change of the feature set and
needs a new feature-set version and new golden vectors, a deliberate, reviewed act. The same two guards, the golden
vectors and the tokenization-parity test, are the parity policy between the analyzer and the trainer.

**Error Handling**: `Create` throws `ArgumentNullException` for a null text and `ArgumentOutOfRangeException` for an
empty or out-of-range span; `Extract` throws `ArgumentOutOfRangeException` for a target outside the sentence and
`InvalidOperationException` for a buffer that is too small. No other input can fail: any text, any tag name, any
confidence (including a NaN) and any sentence length are described and extracted. No exception carries text.

**Dependencies**: the framework only (`Rune`, spans). The inventory and the classifier depend on this unit (the hash
functions), not the other way round.

**Callers**: `HeteronymClassifier` (extraction), `ReadingInventory` (the salts and the folded hash),
`EnglishTextAnalyzer` (describing the tokens of a sentence with `Create`), the later trainer, and the tests.
