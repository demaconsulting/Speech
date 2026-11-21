package SpeechOnnxNemotronStt_DitherNoise {
    private import SoftwareItemMetadata::*;

    part def DitherNoise {
        doc /* Seeded Gaussian dither source applied to the limited audio so digital silence does not produce an
             * exactly-zero log-mel input. The same seed yields the same sequence, the sequence can be restarted, and
             * zero amplitude leaves samples unchanged. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/DitherNoise.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/RecognitionSubsystem/DitherNoiseTests.cs");
            designFiles = ("docs/design/speech-onnx-nemotron-stt/recognition-subsystem/dither-noise.md");
            verificationFiles = ("docs/verification/speech-onnx-nemotron-stt/recognition-subsystem/dither-noise.md");
            requirementFiles = ("docs/reqstream/speech-onnx-nemotron-stt/recognition-subsystem/dither-noise.yaml");
        }
    }
}
