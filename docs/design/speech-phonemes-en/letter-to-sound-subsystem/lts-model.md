### LtsModel

**Purpose**: Convert the spelling of a word to a pronunciation with the model trained on the CMU Pronouncing Dictionary:
the phones of the word and the stress of its vowels. The unit is the only part of the package whose answers are learned
instead of reviewed, so it is built to be exact, immutable and checkable.

**Data Model**:

*The image* is the decompressed content of `lts-en.bin.gz`, little-endian: the magic `LTS1`, the chunk table (a 16-bit
count, then for each chunk a length byte and that many base-phone ids; a chunk is zero, one or two phones), and three
networks in the order phone, primary stress, secondary stress. A network is stored as its group cardinalities, its
hidden widths and output count, its embedding rows (16-bit), the first bias and multiplier (32-bit), the second layer's
weights (8-bit) with its bias and multiplier, and the output weights (8-bit) and bias. The loader checks the header, the
chunk table, the expected group sizes and output counts and that no data trails the networks.

*The network* (`LtsNetwork`) sums the embedding rows selected by the categorical features of its input groups, applies a
ReLU with an exact rescale (`ActivationMax` 2047, shift 24), a second dense layer with the same activation and a dense
output, and returns the integer scores; the caller takes the argmax. Every operation is an integer operation, with
`Vector<int>` used only to speed up sums whose result does not depend on the width.

*The constants* of the feature layout (the letter ids 1 to 26 and 27 for the apostrophe, the window of six letters on
each side, the three-chunk history, the maximum of 127 chunk classes) are part of the format; the trainer in the tools
project uses the same code, through `InternalsVisibleTo`, so the two cannot disagree.

**Key Methods**:

- **FromGZipStream(stream)** and **FromImage(bytes)**: load and validate a model; the embedded model is `Default`,
  loaded on first use under `Lazy<T>`
- **Convert(word)**: maps each letter to its id (letters of either case; anything else is ignored), runs the phone
  network greedily from left to right to build the phone string, then the primary network over every vowel (the
  best-scoring vowel receives primary stress, with one vowel it is certain), then the secondary network over the others,
  and returns the pronunciation
- **PhoneFeatures**, **StressFeatures**: the feature extraction, public to the assembly so that the trainer builds the
  same features

**Error Handling**: a damaged image (wrong header, a chunk table that is empty or too large, a chunk with more than two
phones or an unknown phone id, a chunk table or network layer sizes that the remaining data cannot hold (checked in
64-bit arithmetic before anything is allocated from them), networks with the wrong outputs, a truncated or over-long
image) throws `InvalidDataException`; a null argument throws `ArgumentNullException`. `Convert` cannot fail for any
input.

**Dependencies**: `Phone`/`Pronunciation` (the output) and the framework.

**Callers**: `LetterToSound` and the tools project (training, benchmark and golden regeneration).
