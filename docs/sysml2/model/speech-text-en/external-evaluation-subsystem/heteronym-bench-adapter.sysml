package SpeechTextEn {
    part def HeteronymBenchAdapter {
        doc /* Reader of the pinned English read set of heteronym-bench: parses the items with their strata
             * and labels, skips the items copied from WikipediaHomographData as duplicates, repairs a span
             * that counts bytes when that gives the target word, and skips what cannot be placed. A
             * malformed document is an error that names the item. */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/ExternalEvaluationSubsystem/HeteronymBenchAdapter.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/ExternalEvaluationSubsystem/HeteronymBenchAdapterTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/external-evaluation-subsystem/heteronym-bench-adapter.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/external-evaluation-subsystem/heteronym-bench-adapter.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/external-evaluation-subsystem/heteronym-bench-adapter.yaml */
    }
}
