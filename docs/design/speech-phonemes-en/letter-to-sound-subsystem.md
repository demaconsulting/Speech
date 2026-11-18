## LetterToSoundSubsystem Design

![LetterToSoundSubsystem Structure](PhonemesEnLetterToSoundSubsystemView.svg)

### Overview

The SpeechPhonemesEn LetterToSoundSubsystem pronounces a word that is in neither the dictionary nor derivable from it by
the morphology rules: a name, a brand, a new word. It answers one question, "what does this spelling sound like", and
guarantees that a word with letters is never silent. It contains the following units:

- **LtsModel**: the trained model: the reader of its binary image, the feature extraction and the exact integer network
  (`LtsNetwork`) that chooses the phones and the stresses of a word
- **LetterToSound**: the converter around the model: a bounded cache, the rule that a result with no vowel is spoken by
  letter names, and the handling of text that has no letter

### Interfaces

Both units are internal. `LetterToSound.Default` is the shared converter; `TryConvert(word, out pronunciation)` is the
one question the phonemizer asks, and it returns `false` only for a text without a letter. `LtsModel.Default` is the
shared model, `FromGZipStream` and `FromImage` load one, and `Convert(word)` returns the pronunciation. The subsystem
uses the phone types and the letter names of the LexiconSubsystem and nothing else; the PhonemizerSubsystem (through
`WordPronouncer`) and the tests use it.

### Design

**Model choice.** The model is our own, trained on the pinned CMU Pronouncing Dictionary alone by the non-shipped tools
command `train-lts`. It was chosen by a benchmark on a stem-family-grouped held-out split of the dictionary (the
families of inflected and prefixed forms stay on one side, so the test words are not near-copies of training words): its
phone error rate with stress is 11.6 percent against 25.5 percent for a rule-based baseline kept in the tools project (a
ratio of 45 percent, under the 60 percent the owner decided), with 57.7 percent of the words exactly right; the resource
is 0.67 MB, inference takes about 0.09 ms per word, and the result is deterministic. A pre-trained model from another
project was measured as an upper bound and rejected because the license and provenance of its weights could not be
verified in writing. The full numbers are in `docs/evaluation/phonemes-en/oov.md`.

**Inference.** A word is converted in three steps, each by a small multilayer perceptron whose arithmetic is integer
only: the first network chooses, from left to right, the chunk of zero, one or two phones of each letter from a window
of thirteen letters around it, the distance from both word ends, the previous three chunk classes and the first and last
three letters; the second network scores each vowel of the result as the carrier of the primary stress and the best one
wins (a word with one vowel always has it); the third decides for each other vowel whether it has secondary stress, and
every other vowel is unstressed. The embeddings are 16-bit, the dense weights 8-bit and the activations 0 to 2047 with
an exact rescale, so the answer is bit-identical on every machine and for every vector width the runtime uses.

**Never silent.** A result without a vowel (an initialism such as `www` or `sq`, an interjection such as `hmm`) is not
something a voice can say as a word, so the converter speaks the word letter by letter by letter name. Characters that
are not letters or apostrophes are ignored, apostrophes at the ends of a word are trimmed, and a text with no letter has
no pronunciation (the phonemizer treats it as unsupported).

**Cache.** `LetterToSound` keeps a bounded, thread-safe map from word to pronunciation (4,096 words of at most 40
letters; when it is full new words are simply not cached). The answer depends on the word alone, so the cache only saves
time and never changes a result.

**Error handling.** A damaged model image throws `InvalidDataException` when the model loads, which fails the first use
of the shared instance (a defective build). Conversion never throws for any string, including a word of thousands of
letters.
