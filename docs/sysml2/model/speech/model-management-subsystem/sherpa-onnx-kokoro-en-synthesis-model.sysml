package Speech {
    part def SherpaOnnxKokoroEnglishSynthesisModel {
        doc /* Real, concrete ISynthesisModel backing sherpa-onnx's Kokoro text-to-speech
             * archive kokoro-int8-en-v0_19 (Apache-2.0, confirmed directly from the archive's
             * own LICENSE file), an English-only, int8-quantized, StyleTTS2-derived
             * multi-speaker model. Declares a single .tar.bz2 download, delegates InstallAsync
             * to the shared TarBz2ArchiveExtractor built in Phase 7a, builds the proven Kokoro
             * offline text-to-speech configuration for this model, and - new in Phase 10 - owns
             * a confirmed voice-name-to-speaker-id mapping via ISynthesisModel.ResolveSpeakerId,
             * closing the "speaker selection is out of scope" gap the sibling
             * SherpaOnnxVitsLibriTtsEnglishSynthesisModel explicitly deferred. Declares 11
             * genuinely distinct voices as a ChoiceParameter, confirmed against upstream
             * sherpa-onnx's own generate_voices_bin.py script, byte arithmetic against the real
             * voices.bin, and a live loaded model's NumSpeakers. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/SherpaOnnxKokoroEnglishSynthesisModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SherpaOnnxKokoroEnglishSynthesisModelTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/sherpa-onnx-kokoro-en-synthesis-model.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/sherpa-onnx-kokoro-en-synthesis-model.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/sherpa-onnx-kokoro-en-synthesis-model.yaml */
    }
}
