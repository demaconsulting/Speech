package SpeechTextEn {
    part def HeteronymSubsystem {
        doc /* The English heteronym reading support: the reading inventory (which readings a word
             * such as record can have, with the public ReadingInventory), the deterministic feature
             * extraction that turns a tagged sentence into hashed features, the sparse
             * averaged-perceptron model format with its strict loader, and the classifier that
             * picks one of a word's candidate readings with a calibrated confidence. Only
             * ReadingInventory is public; everything else is internal. The inventory tables are
             * embedded in the package, but no trained model is embedded in this release, so the
             * classifier is effective only when a model exists. Production code of this subsystem
             * is independent of the Analysis subsystem, the DemaConsulting.PartOfSpeech package and
             * the diagnostics sink; the Analysis subsystem depends on it, never the reverse. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Text.En/HeteronymSubsystem/NamespaceDoc.cs */
        comment designRef /* Design: docs/design/speech-text-en/heteronym-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/heteronym-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/heteronym-subsystem.yaml */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Tests/HeteronymSubsystem/HeteronymSubsystemTests.cs */

        part readingInventory : ReadingInventory;
        part heteronymFeatureExtractor : HeteronymFeatureExtractor;
        part sparsePerceptronModel : SparsePerceptronModel;
        part heteronymClassifier : HeteronymClassifier;
    }
}
