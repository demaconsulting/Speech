package Speech {
    part def SherpaOnnxNemotronStreamingEnRecognitionModel {
        doc /* Real, concrete IRecognitionModel backing sherpa-onnx's NVIDIA Nemotron
             * cache-aware FastConformer-RNNT streaming transducer model
             * nemotron-speech-streaming-en-0.6b-560ms-int8-2026-04-25, under the NVIDIA Open
             * Model License (a custom, non-OSI license, visibly distinct from every other
             * model in this pass). Structurally near-identical to
             * SherpaOnnxZipformerEnRecognitionModel: same Transducer config shape, same
             * TarBz2ArchiveExtractor-based InstallAsync; deliberately leaves ModelType unset
             * since the native library auto-detects the decoding path from the decoder file
             * itself. Stores metadata and a download descriptor only - never bundles or
             * redistributes NVIDIA's model weights. Empirically confirmed (real
             * LibriTTS-synthesized audio through the real recognizer) to already produce
             * cased, punctuated-ish prose, so it keeps IRecognitionModel's pass-through
             * NormalizeText default instead of applying UppercaseTranscriptRestorer. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/SherpaOnnxNemotronStreamingEnRecognitionModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SherpaOnnxNemotronStreamingEnRecognitionModelTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/sherpa-onnx-nemotron-streaming-en-recognition-model.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/sherpa-onnx-nemotron-streaming-en-recognition-model.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/sherpa-onnx-nemotron-streaming-en-recognition-model.yaml */
    }
}
