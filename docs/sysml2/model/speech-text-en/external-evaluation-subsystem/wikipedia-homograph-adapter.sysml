package SpeechTextEn {
    part def WikipediaHomographAdapter {
        doc /* Reader of the pinned Google WikipediaHomographData files: the tab-separated files with
             * CSV-style quoted fields, converts the byte offsets to character offsets, skips a row whose
             * converted span is not its homograph, and reads the training and evaluation splits from their
             * own pinned files in ordinal order through the verified cache. */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/ExternalEvaluationSubsystem/WikipediaHomographAdapter.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/ExternalEvaluationSubsystem/WikipediaHomographAdapterTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/external-evaluation-subsystem/wikipedia-homograph-adapter.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/external-evaluation-subsystem/wikipedia-homograph-adapter.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/external-evaluation-subsystem/wikipedia-homograph-adapter.yaml */
    }
}
