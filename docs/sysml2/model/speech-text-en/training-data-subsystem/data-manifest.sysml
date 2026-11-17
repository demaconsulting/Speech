package SpeechTextEn {
    part def DataManifest {
        doc /* Verifier of the committed manifest of the training data: every listed file exists with
             * the recorded SHA-256, byte count and row count, every file of the folder is listed (except
             * the manifest and the local cache folders), and the entries are unique, sorted and in
             * canonical form. */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/TrainingDataSubsystem/DataManifest.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/DataManifestTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/training-data-subsystem/data-manifest.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/training-data-subsystem/data-manifest.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/training-data-subsystem/data-manifest.yaml */
    }
}
