package Speech {
    part def UppercaseTranscriptRestorer {
        doc /* Shared internal helper that turns a streaming recognizer's UPPERCASE,
             * unpunctuated raw output into readable prose: restores a closed, conservative
             * set of contractions, capitalizes the first letter and standalone "I", and
             * ensures terminal punctuation. Ported from the reference
             * HiArc.AI.Speech.DictationRestorer implementation, dropping its
             * glossary-casing and trained-punctuation-model stages entirely. Used by
             * SherpaOnnxZipformerEnRecognitionModel's IRecognitionModel.NormalizeText
             * override; reusable by any other model whose raw output empirically matches
             * this "yelling" style. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/UppercaseTranscriptRestorer.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/UppercaseTranscriptRestorerTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/uppercase-transcript-restorer.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/uppercase-transcript-restorer.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/uppercase-transcript-restorer.yaml */
    }
}
