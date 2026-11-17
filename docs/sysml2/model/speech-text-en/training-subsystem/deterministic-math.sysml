package SpeechTextEn {
    part def DeterministicMath {
        doc /* Exponential and natural logarithm computed from basic arithmetic in a fixed order, so the
             * calibration-temperature choice never depends on a platform's math library. */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/TrainingSubsystem/DeterministicMath.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/DeterministicMathTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/training-subsystem/deterministic-math.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/training-subsystem/deterministic-math.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/training-subsystem/deterministic-math.yaml */
    }
}
