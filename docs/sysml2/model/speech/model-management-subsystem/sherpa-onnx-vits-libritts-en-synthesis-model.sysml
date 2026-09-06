package Speech {
    part def SherpaOnnxVitsLibriTtsEnglishSynthesisModel {
        doc /* Real, concrete ISynthesisModel backing sherpa-onnx's VITS/Piper text-to-speech
             * voice vits-piper-en_US-libritts_r-medium (CC BY 4.0, confirmed directly from this
             * model's own MODEL_CARD). Declares a single .tar.bz2 download, delegates
             * InstallAsync to the shared TarBz2ArchiveExtractor built in Phase 7a, builds the
             * proven VITS offline text-to-speech configuration for this model, and owns a plain
             * numeric speaker-index NumericParameter (0-903) plus a matching
             * ISynthesisModel.ResolveSpeakerId override, since LibriTTS-R's 904 speaker
             * embeddings have no published human-readable name mapping - closing the gap
             * previously deferred as a known, accepted, out-of-scope limitation. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/SherpaOnnxVitsLibriTtsEnglishSynthesisModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SherpaOnnxVitsLibriTtsEnglishSynthesisModelTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/sherpa-onnx-vits-libritts-en-synthesis-model.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/sherpa-onnx-vits-libritts-en-synthesis-model.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/sherpa-onnx-vits-libritts-en-synthesis-model.yaml */
    }
}
