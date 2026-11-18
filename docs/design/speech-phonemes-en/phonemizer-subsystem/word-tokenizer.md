### WordTokenizer

**Purpose**: Prepare the text for pronunciation: build the spoken text from the original text and its replacement
annotations, index the reading and pronunciation annotations by the original span they cover, and cut the spoken text
into tokens. Three internal classes serve this one responsibility: `SpokenText`, `AnnotationIndex` and `WordTokenizer`.

**Data Model**:

- `SpokenText`: the spoken string `Text`, the original index of each spoken character (`-1` for a character that a
  replacement introduced), the end of the original span each character stands for and whether each character belongs to
  a spelled replacement. Immutable once built
- `AnnotationIndex`: the reading ids and the pronunciation overrides (scheme and phonemes) keyed by the original span
  (`Start`, `Length`); at most one of each kind per span, the first kept
- `Token(Kind, Start, End)` with `TokenKind`: `Word`, `Letter`, `Digits`, `Clitic`, `Punctuation`, `Unsupported`

**Key Methods**:

- **SpokenText.Build(text, annotations)**: applies the `en.norm` and `en.spell` annotations in order of start; a
  replacement that starts before the end of the previous one, that is out of range or whose value is malformed is
  skipped; the replacement value is appended in place of the span (an empty value removes it) and a spelled value marks
  its characters as spelled letters
- **SpokenText.TryGetSourceSpan(start, end)**: the original start and the whole original length of a spoken range and
  the length of its unchanged headword (the leading characters that are original text in order and without a gap), when
  the range starts with original text, which is the condition for a reading or pronunciation annotation to apply to a
  token or to its headword
- **AnnotationIndex.Build(annotations)**: reads each annotation with the typed readers of the text analysis package
  (`TryReadReading`, `TryReadPronunciation`); a malformed annotation is not indexed. **TryGetReading** also accepts an
  annotation that covers only the start of a token and reports the covered length
- **WordTokenizer.Tokenize(spoken, lexicon)**: the rules in the order given in *PhonemizerSubsystem Design*: skip
  whitespace and U+FFFC; a spelled letter; digits; a word (Latin letters with inner apostrophes and hyphens, and a
  trailing apostrophe after `s`); a leading apostrophe as part of a word only when the lexicon lists the word so; a
  clitic after a replacement boundary; punctuation as a run of one mark; otherwise an unsupported run. A token never
  contains whitespace and the tokens, with the skipped characters, cover the whole spoken text

**Error Handling**: `Build` throws `ArgumentNullException` for a null text; nothing else can fail. A bad annotation is
skipped, never thrown.

**Dependencies**: `CmuLexicon` (the leading-apostrophe rule), the typed annotation readers of SpeechTextEn and the
Speech `SegmentAnnotation`.

**Callers**: `EnglishPhonemizer` and the tests.
