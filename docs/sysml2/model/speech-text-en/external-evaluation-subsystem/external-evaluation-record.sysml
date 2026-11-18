package SpeechTextEn {
    part def ExternalEvaluationRecord {
        doc /* Renderer and verifier of the external evaluation record: seven deterministic tab-separated
             * files (sources, conversion, summary, by-source, per-reading, confidence, seeds) of integers and
             * basis points with LF line ends and no time stamps, written to a folder and compared with it
             * byte for byte. They hold only numbers, pins and our own reading identifiers, never a sentence,
             * word or label of an external dataset. Also holds the ExternalEvaluationReport,
             * ExternalSetResult and ExternalSeedResult records. */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/ExternalEvaluationSubsystem/ExternalEvaluationRecord.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/ExternalEvaluationSubsystem/ExternalEvaluationRecordTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/external-evaluation-subsystem/external-evaluation-record.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/external-evaluation-subsystem/external-evaluation-record.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/external-evaluation-subsystem/external-evaluation-record.yaml */
    }
}
