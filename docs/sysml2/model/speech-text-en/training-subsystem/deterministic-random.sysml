package SpeechTextEn {
    part def DeterministicRandom {
        doc /* SplitMix64 pseudo-random generator with exactly uniform bounded draws and a Fisher-Yates
             * shuffle, so training row order and bootstrap draws depend only on the seed and never on the
             * runtime or the operating system. */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/TrainingSubsystem/DeterministicRandom.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/DeterministicRandomTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/training-subsystem/deterministic-random.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/training-subsystem/deterministic-random.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/training-subsystem/deterministic-random.yaml */
    }
}
