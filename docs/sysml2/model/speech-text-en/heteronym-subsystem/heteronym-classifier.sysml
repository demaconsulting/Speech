package SpeechTextEn {
    part def HeteronymClassifier {
        doc /* The internal classifier that chooses which reading of an inventory form a word
             * occurrence has: scores only the form's candidate readings with the sparse
             * perceptron model (so a reading that is not a candidate can never win), breaks exact
             * ties by inventory row order, and reports a temperature-calibrated confidence; a form
             * with one candidate is returned with confidence 1 without touching the model. The
             * decision uses integers only. It also holds the lazy, once-only model provider that
             * opens and loads the model on first use and records a missing, invalid or
             * incompatible model as a cached status instead of throwing. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Text.En/HeteronymSubsystem/HeteronymClassifier.cs, src/DemaConsulting.Speech.Text.En/HeteronymSubsystem/HeteronymModelProvider.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Tests/HeteronymSubsystem/HeteronymClassifierTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/heteronym-subsystem/heteronym-classifier.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/heteronym-subsystem/heteronym-classifier.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/heteronym-subsystem/heteronym-classifier.yaml */
    }
}
