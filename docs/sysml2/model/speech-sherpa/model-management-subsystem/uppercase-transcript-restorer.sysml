package SpeechSherpa_UppercaseTranscriptRestorer {
    private import SoftwareItemMetadata::*;

    part def UppercaseTranscriptRestorer {
        doc /* Shared internal helper that turns a streaming recognizer's UPPERCASE, unpunctuated raw output into
             * readable prose: restores a closed, conservative set of contractions, capitalizes the first letter and
             * standalone "I", and ensures terminal punctuation. Ported from the reference
             * HiArc.AI.Speech.DictationRestorer implementation, dropping its glossary-casing and
             * trained-punctuation-model stages entirely. Used by SherpaOnnxZipformerEnRecognitionModel's
             * IRecognitionModel.NormalizeText override; reusable by any other model whose raw output empirically
             * matches this "yelling" style. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Sherpa/ModelManagementSubsystem/UppercaseTranscriptRestorer.cs");
            testFiles = ("test/DemaConsulting.Speech.Sherpa.Tests/ModelManagementSubsystem/UppercaseTranscriptRestorerTests.cs");
            designFiles = ("docs/design/speech-sherpa/model-management-subsystem/uppercase-transcript-restorer.md");
            verificationFiles = ("docs/verification/speech-sherpa/model-management-subsystem/uppercase-transcript-restorer.md");
            requirementFiles = ("docs/reqstream/speech-sherpa/model-management-subsystem/uppercase-transcript-restorer.yaml");
        }
    }
}
