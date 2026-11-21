### PiperPhonemePipeline Verification

#### Verification Approach

`PiperPhonemePipeline` is verified by unit tests in
`test/DemaConsulting.Speech.Onnx.Piper.Tests/SynthesisSubsystem/PiperPhonemePipelineTests.cs` with the model's real
English text analysis, the session's own sentence chunking, the real phonemizer and renderer and the real symbol table
of the voice (a trimmed copy of its configuration); no ONNX graph is involved. Four corpora are the evidence:

- the renderer's own Piper golden (`piper-ipa-golden.tsv`, linked from the Phonemes.En tests): 150 sentences, every
  symbol of which must be in the voice's table and encode to the ids pinned there;
- the pipeline golden (`TestData/piper-pipeline-golden.tsv`): the 41 sentences of the Kokoro pipeline corpus (linked
  from the Kokoro tests), 12 of which carry both readings of a heteronym such as `record` or `wind`, run the way a
  session runs them (analyze, chunk with the annotations, speak each chunk) and pinned with their phoneme string and
  framed ids. A change in this golden means the audio changes, so it is reviewed by listening and not only by reading;
- the Gettysburg golden (`TestData/piper-gettysburg-golden.tsv`): five sentences of the Gettysburg Address that a
  listening review of the voice flagged (*battle-field* heard as two words, *here* heard as *he*, *nobly* heard as
  *knobbly*, and a pause inside *the last full measure of | devotion* of a sentence of more than 400 characters), run
  the same way and pinned with the phoneme strings of their utterances, separated by `|`;
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
readings; with the model's own analysis the noun `will` of `my will was` keeps its primary stress mark and without
annotations it has the weak form; every sentence of the Gettysburg golden produces exactly its pinned utterances, each
within 384 symbols with no unknown symbol; the final sentence of the address (more than 400 characters) is cut at its
dashes into at least two utterances of at most 300 characters, none ends in the weak form of a function word,
`of devotion` stays in one utterance and every utterance but the last ends in a pause mark; `Welcome Zorblax` has at
least five more symbols than `Welcome` and no unknown symbol; 40 repeated sentences split into at least three utterances
of at most 384 symbols each with no word lost and each utterance but the last ending after a comma or a period; an empty
text and an emoji give no utterance; a null text and a null configuration are rejected.

#### Test Scenarios

##### Encoding

**Tests**: `PiperPhonemePipeline_Encode_Phonemes_AreFramedWithBeginPadAndEnd`,
`PiperPhonemePipeline_Encode_RendererGoldenSentence_CoversEverySymbolWithThePinnedIds`,
`PiperPhonemePipeline_ToUtterances_RendererCorpusAndValidationSentences_MatchTheRendererPathWithNoUnknownSymbol`,
`PiperPhonemePipeline_Encode_SymbolsOutsideTheTable_AreReportedAndSkipped`

##### Readings and unknown words

**Tests**: `PiperPhonemePipeline_ToUtterances_GoldenSentence_ProducesPinnedPhonemesAndIds`,
`PiperPhonemePipeline_ToUtterances_HeteronymSentence_UsesTheChosenReadings`,
`PiperPhonemePipeline_ToUtterances_ModelAnalysis_KeepsANounOfAFunctionWordStressed`,
`PiperPhonemePipeline_ToUtterances_UnknownWord_IsStillSpoken`,
`PiperPhonemePipeline_ToUtterances_GettysburgSentence_ProducesPinnedPhonemes`

##### Long and soundless text

**Tests**: `PiperPhonemePipeline_ToUtterances_TextBeyondOneUtterance_IsSplitAtClauseBoundariesWithoutLoss`,
`PiperPhonemePipeline_ToUtterances_EmptyOrSoundlessText_ProducesNoUtterancesAndNullIsRejected`,
`PiperPhonemePipeline_ToUtterances_SentenceWithClauses_IsOneUtteranceWithItsCommasInPlace`,
`PiperPhonemePipeline_ToUtterances_LongSentenceOfTheAddress_IsCutAtItsDashesNeverBetweenFunctionWordAndHead`

`Would you like tea, coffee, or something stronger?` is three pieces for the core chunker and one segment through the
model's capability profile, which the pipeline speaks as one utterance with two commas in its phonemes and in its ids; a
sentence of twelve clauses is several segments of at most 300 characters, every utterance within 384 symbols, and the
sounds of all of them together are the sounds of the whole sentence.
