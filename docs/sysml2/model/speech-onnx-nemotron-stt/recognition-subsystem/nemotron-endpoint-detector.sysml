package SpeechOnnxNemotronStt_NemotronEndpointDetector {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxNemotronStt_NemotronFeatureExtractor::*;
    private import ScalarValues::*;

    enum def NemotronEndpointReason {
        enum None;
        enum TrailingSilence;
        enum TrailingSilenceAfterSpeech;
        enum MaxUtteranceLength;
    }

    part def NemotronEndpointDetector {
        doc /* Decides when an utterance ends on the decode clock with the sherpa-onnx online recognizer's rules and
             * defaults: rule 2, 1200 ms of trailing silence after a token; rule 1, 2400 ms of trailing silence in
             * any case; rule 3, a 20000 ms utterance (zero disables a rule, a negative value is rejected). Decoded
             * blank frames and quiet input removed by the silence-run limiter advance the trailing silence and
             * utterance clocks; padding frames never do. Check is pure and evaluates rule 2, then rule 1, then
             * rule 3; Reset starts a new segment, optionally one that carries speech. Also holds the
             * NemotronEndpointRules record and the NemotronEndpointReason enumeration. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/NemotronEndpointDetector.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/RecognitionSubsystem/NemotronEndpointDetectorTests.cs");
            designFiles = ("docs/design/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-endpoint-detector.md");
            verificationFiles = ("docs/verification/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-endpoint-detector.md");
            requirementFiles = ("docs/reqstream/speech-onnx-nemotron-stt/recognition-subsystem/nemotron-endpoint-detector.yaml");
        }

        // The NemotronEndpointRules record (milliseconds; zero disables a rule).
        attribute trailingSilenceMs : Integer;
        attribute trailingSilenceAfterSpeechMs : Integer;
        attribute maxUtteranceMs : Integer;

        // Uses the feature extractor's sample rate as the default rate for converting rule durations to
        // samples.
        dependency NemotronEndpointDetector to NemotronFeatureExtractor;
    }
}
