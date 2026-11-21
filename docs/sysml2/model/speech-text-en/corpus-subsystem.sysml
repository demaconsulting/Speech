package SpeechTextEn_CorpusSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_CorpusCache::*;
    private import SpeechTextEn_CorpusCommand::*;
    private import SpeechTextEn_CorpusLeakageAudit::*;
    private import SpeechTextEn_CorpusReader::*;
    private import SpeechTextEn_GutenbergBody::*;

    part def CorpusSubsystem {
        doc /* The corpus command of the non-shipped Text.En trainer, kept after the word-cluster experiment for later
             * context features and run by hand (CI runs only its tests): the pinned source list of US public-domain
             * books with its mirror-only, rate-limited download (CorpusCache), the pinned body extraction of a
             * downloaded e-book (GutenbergBody), the tokenization of the bodies with the analyzer's own tokenizer
             * (CorpusReader), the content-level leakage audit of the corpus against every evaluation set
             * (CorpusLeakageAudit) and the command line entry (CorpusCommand). Drift of a mirror text is reported
             * and handled by re-pinning; there is no corpus snapshot. No model, feature or setting uses the corpus
             * yet. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/CorpusSubsystemTests.cs");
            designFiles = ("docs/design/speech-text-en/corpus-subsystem.md");
            verificationFiles = ("docs/verification/speech-text-en/corpus-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-text-en/corpus-subsystem.yaml");
        }

        part gutenbergBody : GutenbergBody;
        part corpusCache : CorpusCache;
        part corpusReader : CorpusReader;
        part corpusLeakageAudit : CorpusLeakageAudit;
        part corpusCommand : CorpusCommand;
    }
}
