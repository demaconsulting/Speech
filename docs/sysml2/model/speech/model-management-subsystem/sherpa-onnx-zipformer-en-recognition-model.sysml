package Speech {
    part def SherpaOnnxZipformerEnRecognitionModel {
        doc /* Real, concrete IRecognitionModel backing sherpa-onnx's streaming Zipformer2
             * transducer model streaming-zipformer-en-2023-06-26 (Apache-2.0, likely - not
             * independently confirmed against an explicit upstream LICENSE file). Declares a
             * single .tar.bz2 download, delegates InstallAsync to TarBz2ArchiveExtractor, and
             * builds the proven int8 streaming-recognizer configuration for this model.
             * Overrides NormalizeText to restore casing/contractions/punctuation on this
             * model's raw UPPERCASE, unpunctuated output via the shared
             * UppercaseTranscriptRestorer - confirmed empirically by feeding real
             * LibriTTS-synthesized audio through the real recognizer. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/SherpaOnnxZipformerEnRecognitionModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SherpaOnnxZipformerEnRecognitionModelTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/sherpa-onnx-zipformer-en-recognition-model.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/sherpa-onnx-zipformer-en-recognition-model.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/sherpa-onnx-zipformer-en-recognition-model.yaml */
    }
}
