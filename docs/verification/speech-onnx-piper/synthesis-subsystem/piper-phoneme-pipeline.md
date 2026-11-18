### PiperPhonemePipeline Verification

#### Verification Approach

`PiperPhonemePipeline` is verified by unit tests in
`test/DemaConsulting.Speech.Onnx.Piper.Tests/SynthesisSubsystem/PiperPhonemePipelineTests.cs` with the model's real
English text analysis, the session's own sentence chunking, the real phonemizer and renderer and the real symbol table
of the voice (a trimmed copy of its configuration); no ONNX graph is involved. Three corpora are the evidence:

- the renderer's own Piper golden (`piper-ipa-golden.tsv`, linked from the Phonemes.En tests): 150 sentences, every
  symbol of which must be in the voice's table and encode to the ids pinned there;
- the pipeline golden (`TestData/piper-pipeline-golden.tsv`): the 40 sentences of the Kokoro pipeline corpus (linked
  from the Kokoro tests), 12 of which carry both readings of a heteronym such as `record` or `wind`, run the way a
  session runs them (analyze, chunk with the annotations, speak each chunk) and pinned with their phoneme string and
  framed ids. A change in this golden means the audio changes, so it is reviewed by listening and not only by reading;
- the 300 tuning and 60 validation sentences of the renderer's measurements (the repository's `data/phonemes-en` folder,
  linked): for every one the path through the pipeline equals the renderer's path and no symbol is unknown.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Files**: the trimmed configuration, the pipeline golden and the linked corpora, copied next to the test assembly
- **Isolation**: no network, no model; the pipeline is immutable and shared by the tests of the class

#### Acceptance Criteria

Every sentence of the renderer golden encodes with no unknown symbol, the pinned ids and the same phoneme string as the
pipeline gives for it; the 360 corpus sentences match the renderer's path with no unknown symbol; `hˈ` encodes as the
ids `1, h, 0, ˈ, 0, 2` with two symbols; the text `h中中hÿ` reports `中` and `ÿ` in that order as unknown, two encoded
symbols and six ids; every sentence of the pipeline golden produces exactly its pinned phonemes and ids with no unknown
symbol; in each of the twelve heteronym sentences the ids with the annotations differ from the ids with default
readings; `Welcome Zorblax` has at least five more symbols than `Welcome` and no unknown symbol; 40 repeated sentences
split into at least three utterances of at most 256 symbols each with no word lost and each utterance but the last
ending after a comma or a period; an empty text and an emoji give no utterance; a null text and a null configuration are
rejected.

#### Test Scenarios

##### Encoding

**Tests**: `PiperPhonemePipeline_Encode_Phonemes_AreFramedWithBeginPadAndEnd`,
`PiperPhonemePipeline_Encode_RendererGoldenSentence_CoversEverySymbolWithThePinnedIds`,
`PiperPhonemePipeline_ToUtterances_RendererCorpusAndValidationSentences_MatchTheRendererPathWithNoUnknownSymbol`,
`PiperPhonemePipeline_Encode_SymbolsOutsideTheTable_AreReportedAndSkipped`

##### Readings and unknown words

**Tests**: `PiperPhonemePipeline_ToUtterances_GoldenSentence_ProducesPinnedPhonemesAndIds`,
`PiperPhonemePipeline_ToUtterances_HeteronymSentence_UsesTheChosenReadings`,
`PiperPhonemePipeline_ToUtterances_UnknownWord_IsStillSpoken`

##### Long and soundless text

**Tests**: `PiperPhonemePipeline_ToUtterances_TextBeyondOneUtterance_IsSplitAtClauseBoundariesWithoutLoss`,
`PiperPhonemePipeline_ToUtterances_EmptyOrSoundlessText_ProducesNoUtterancesAndNullIsRejected`
