package SpeechTextEn_DeterministicRandom {
    private import SoftwareItemMetadata::*;

    part def DeterministicRandom {
        doc /* SplitMix64 pseudo-random generator with exactly uniform bounded draws and a Fisher-Yates shuffle, so
             * training row order and bootstrap draws depend only on the seed and never on the runtime or the
             * operating system. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/TrainingSubsystem/DeterministicRandom.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/DeterministicRandomTests.cs");
            designFiles = ("docs/design/speech-text-en/training-subsystem/deterministic-random.md");
            verificationFiles = ("docs/verification/speech-text-en/training-subsystem/deterministic-random.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-subsystem/deterministic-random.yaml");
        }
    }
}
