package SpeechOnnxNemotronStt_NemotronFeatureExtractor {
    private import SoftwareItemMetadata::*;

    part def NemotronFeatureExtractor {
        doc /* Streaming 128-band log-mel front end reproducing NeMo's FilterbankFeatures inference path: 0.97
             * preemphasis, a constant (zero) center pad of 256 samples, 512-point FFT of a 400-sample symmetric Hann
             * window, hop of 160 samples, Slaney mel filters, and log(mel + 2^-24), giving len / 160 + 1 frames for
             * len samples. Frames are emitted while streaming, and streaming output equals one-shot output. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/NemotronFeatureExtractor.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/RecognitionSubsystem/NemotronFeatureExtractorTests.cs");
            designFiles = ("docs/design/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-feature-extractor.md");
            verificationFiles = ("docs/verification/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-feature-extractor.md");
            requirementFiles = ("docs/reqstream/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-feature-extractor.yaml");
        }
    }
}
