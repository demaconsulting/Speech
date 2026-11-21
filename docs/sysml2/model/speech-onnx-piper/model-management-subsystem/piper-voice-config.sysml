package SpeechOnnxPiper_PiperVoiceConfig {
    private import SoftwareItemMetadata::*;

    part def PiperVoiceConfig {
        doc /* The reader of a Piper voice's .onnx.json file: the symbol table that turns phoneme symbols into the
             * integer ids the graph takes (with the required framing symbols ^, _ and $), the output sample rate, the
             * speaker count and the voice's recommended inference scales. Internal; rejects an unusable configuration
             * with an invalid-data error that names what is wrong. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.Piper/ModelManagementSubsystem/PiperVoiceConfig.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.Piper.Tests/ModelManagementSubsystem/PiperVoiceConfigTests.cs");
            designFiles = ("docs/design/speech-onnx-piper/model-management-subsystem/piper-voice-config.md");
            verificationFiles = ("docs/verification/speech-onnx-piper/model-management-subsystem/piper-voice-config.md");
            requirementFiles = ("docs/reqstream/speech-onnx-piper/model-management-subsystem/piper-voice-config.yaml");
        }
    }
}
