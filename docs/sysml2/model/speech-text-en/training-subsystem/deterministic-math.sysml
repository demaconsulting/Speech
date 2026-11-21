package SpeechTextEn_DeterministicMath {
    private import SoftwareItemMetadata::*;

    part def DeterministicMath {
        doc /* Exponential and natural logarithm computed from basic arithmetic in a fixed order, so the
             * calibration-temperature choice never depends on a platform's math library. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/TrainingSubsystem/DeterministicMath.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/DeterministicMathTests.cs");
            designFiles = ("docs/design/speech-text-en/training-subsystem/deterministic-math.md");
            verificationFiles = ("docs/verification/speech-text-en/training-subsystem/deterministic-math.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-subsystem/deterministic-math.yaml");
        }
    }
}
