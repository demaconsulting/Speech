package SpeechSherpa_SherpaOnnxNemotronStreamingEnRecognitionModel {
    private import SoftwareItemMetadata::*;
    private import SpeechSherpa_SherpaOnnxRecognitionEngine::*;
    private import SpeechSherpa_TarBz2ArchiveExtractor::*;

    part def SherpaOnnxNemotronStreamingEnRecognitionModel {
        doc /* Real, concrete IRecognitionModel backing sherpa-onnx's NVIDIA Nemotron cache-aware FastConformer-RNNT
             * streaming transducer model nemotron-speech-streaming-en-0.6b-560ms-int8-2026-04-25, under the NVIDIA
             * Open Model License (a custom, non-OSI license, visibly distinct from every other model in this set).
             * Structurally near-identical to SherpaOnnxZipformerEnRecognitionModel: same Transducer config shape,
             * same TarBz2ArchiveExtractor-based InstallAsync; deliberately leaves ModelType unset since the native
             * library auto-detects the decoding path from the decoder file itself. Stores metadata and a download
             * descriptor only - never bundles or redistributes NVIDIA's model weights. Empirically confirmed (real
             * LibriTTS-synthesized audio through the real recognizer) to already produce cased, punctuated-ish prose,
             * so it keeps IRecognitionModel's pass-through NormalizeText default instead of applying
             * UppercaseTranscriptRestorer. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Sherpa/ModelManagementSubsystem/SherpaOnnxNemotronStreamingEnRecognitionModel.cs");
            testFiles = ("test/DemaConsulting.Speech.Sherpa.Tests/ModelManagementSubsystem/SherpaOnnxNemotronStreamingEnRecognitionModelTests.cs");
            designFiles = ("docs/design/speech-sherpa/model-management-subsystem/sherpa-onnx-nemotron-streaming-en-recognition-model.md");
            verificationFiles = ("docs/verification/speech-sherpa/model-management-subsystem/sherpa-onnx-nemotron-streaming-en-recognition-model.md");
            requirementFiles = ("docs/reqstream/speech-sherpa/model-management-subsystem/sherpa-onnx-nemotron-streaming-en-recognition-model.yaml");
        }

        // Declares the contract and archive, extracts it, and wraps the sherpa-onnx recognizer in the
        // engine.
        dependency SherpaOnnxNemotronStreamingEnRecognitionModel to SherpaOnnxRecognitionEngine;
        dependency SherpaOnnxNemotronStreamingEnRecognitionModel to TarBz2ArchiveExtractor;
        dependency SherpaOnnxNemotronStreamingEnRecognitionModel to Speech_AudioFormat::AudioFormat;
        dependency SherpaOnnxNemotronStreamingEnRecognitionModel to Speech_RecognitionBackend::RecognitionBackend;
        dependency SherpaOnnxNemotronStreamingEnRecognitionModel to Speech_SpeechModelContract::SpeechModelContract;
        dependency SherpaOnnxNemotronStreamingEnRecognitionModel to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency SherpaOnnxNemotronStreamingEnRecognitionModel to Speech_SpeechModelDownloadDescriptor::SpeechModelDownloadDescriptor;
        dependency SherpaOnnxNemotronStreamingEnRecognitionModel to Speech_SpeechModelDownloadFile::SpeechModelDownloadFile;
        dependency SherpaOnnxNemotronStreamingEnRecognitionModel to Speech_SpeechModelParameters::SpeechModelParameters;
        dependency SherpaOnnxNemotronStreamingEnRecognitionModel to OtsDependencies::SherpaOnnx;
    }
}
