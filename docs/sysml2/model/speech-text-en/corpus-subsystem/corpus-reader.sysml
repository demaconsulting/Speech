package SpeechTextEn_CorpusReader {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_PartOfSpeechAdapter::*;

    part def CorpusReader {
        doc /* Splits the pinned corpus bodies into paragraphs, sentences and tokens with the analyzer's own tokenizer
             * and lower-case rule, so the leakage audit compares the corpus with the evaluation sentences on the
             * analyzer's terms. Also holds the CorpusParagraph, CorpusBook and TokenizedCorpus records. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/CorpusSubsystem/CorpusReader.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/CorpusSubsystem/CorpusReaderTests.cs");
            designFiles = ("docs/design/speech-text-en/corpus-subsystem/corpus-reader.md");
            verificationFiles = ("docs/verification/speech-text-en/corpus-subsystem/corpus-reader.md");
            requirementFiles = ("docs/reqstream/speech-text-en/corpus-subsystem/corpus-reader.yaml");
        }

        // Tokenizes the corpus with the analyzer's own sentence and token split.
        dependency CorpusReader to PartOfSpeechAdapter;
    }
}
