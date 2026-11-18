package SpeechOnnxPiper {
    part def PiperVoiceConfig {
        doc /* The reader of a Piper voice's .onnx.json file: the symbol table that turns phoneme symbols
             * into the integer ids the graph takes (with the required framing symbols ^, _ and $), the
             * output sample rate, the speaker count and the voice's recommended inference scales. Internal;
             * rejects an unusable configuration with an invalid-data error that names what is wrong. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.Piper/ModelManagementSubsystem/PiperVoiceConfig.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.Piper.Tests/ModelManagementSubsystem/PiperVoiceConfigTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-piper/model-management-subsystem/piper-voice-config.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-piper/model-management-subsystem/piper-voice-config.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-piper/model-management-subsystem/piper-voice-config.yaml */
    }
}
