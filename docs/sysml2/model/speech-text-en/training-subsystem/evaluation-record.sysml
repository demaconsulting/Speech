package SpeechTextEn {
    part def EvaluationRecord {
        doc /* Renderer and verifier of the evaluation record: six deterministic tab-separated files
             * (summary, gate, by-source, per-reading, confidence, seeds) of integers and basis points with
             * LF line ends and no time stamps, written to a folder and compared with it byte for byte.
             * Tab-separated rather than Markdown so the repository formatters cannot reflow them. Also
             * holds the EvaluationReport record. */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/TrainingSubsystem/EvaluationRecord.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/EvaluationRecordTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/training-subsystem/evaluation-record.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/training-subsystem/evaluation-record.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/training-subsystem/evaluation-record.yaml */
    }
}
