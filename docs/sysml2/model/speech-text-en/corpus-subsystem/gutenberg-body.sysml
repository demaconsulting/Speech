package SpeechTextEn_GutenbergBody {
    private import SoftwareItemMetadata::*;

    part def GutenbergBody {
        doc /* Extracts the body of a downloaded plain-text e-book (strictly between the archive's START and END
             * marker lines, without the leading credit and archive-name paragraphs) and normalizes it to the exact
             * bytes that are pinned, a pure function of the downloaded bytes so the same download gives the same body
             * pin on every platform. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/CorpusSubsystem/GutenbergBody.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/CorpusSubsystem/GutenbergBodyTests.cs");
            designFiles = ("docs/design/speech-text-en/corpus-subsystem/gutenberg-body.md");
            verificationFiles = ("docs/verification/speech-text-en/corpus-subsystem/gutenberg-body.md");
            requirementFiles = ("docs/reqstream/speech-text-en/corpus-subsystem/gutenberg-body.yaml");
        }
    }
}
