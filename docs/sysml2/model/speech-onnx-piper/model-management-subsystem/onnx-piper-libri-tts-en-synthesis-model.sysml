package SpeechOnnxPiper {
    part def OnnxPiperLibriTtsEnSynthesisModel {
        doc /* Real, concrete ISynthesisModel for the Piper English (US) voice en_US-libritts_r-medium
             * (CC BY 4.0, 904 speakers, 22,050 Hz), run directly through ONNX Runtime. Declares the stable
             * identifier, the license, the shared English text analyzer and one integer speaker parameter
             * (0 to 903, default 0), owns the download descriptor of the pinned sherpa-onnx .tar.bz2 archive
             * and its installation (the internal PiperArchiveExtractor keeps only the ONNX graph and the
             * voice configuration, flat by bare file name, and deletes the archive), and builds the
             * synthesis engine from the installed files through the sibling SpeechOnnx system's
             * OnnxExecutionProviderSelector, after the voice configuration has been read and checked. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.Piper/ModelManagementSubsystem/OnnxPiperLibriTtsEnSynthesisModel.cs, src/DemaConsulting.Speech.Onnx.Piper/ModelManagementSubsystem/PiperArchiveExtractor.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.Piper.Tests/ModelManagementSubsystem/OnnxPiperLibriTtsEnSynthesisModelTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-piper/model-management-subsystem/onnx-piper-libri-tts-en-synthesis-model.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-piper/model-management-subsystem/onnx-piper-libri-tts-en-synthesis-model.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-piper/model-management-subsystem/onnx-piper-libri-tts-en-synthesis-model.yaml */
    }
}
