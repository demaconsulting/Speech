package SpeechSherpa_SherpaOnnxZipformerEnRecognitionModel {
    private import SoftwareItemMetadata::*;
    private import SpeechSherpa_SherpaOnnxRecognitionEngine::*;
    private import SpeechSherpa_TarBz2ArchiveExtractor::*;
    private import SpeechSherpa_UppercaseTranscriptRestorer::*;

    part def SherpaOnnxZipformerEnRecognitionModel {
        doc /* Real, concrete IRecognitionModel backing sherpa-onnx's streaming Zipformer2 transducer model
             * streaming-zipformer-en-2023-06-26 (Apache-2.0, likely - not independently confirmed against an explicit
             * upstream LICENSE file). Declares a single .tar.bz2 download, delegates InstallAsync to
             * TarBz2ArchiveExtractor, and builds the proven int8 streaming-recognizer configuration for this model.
             * Overrides NormalizeText to restore casing/contractions/punctuation on this model's raw UPPERCASE,
             * unpunctuated output via the shared UppercaseTranscriptRestorer - confirmed empirically by feeding real
             * LibriTTS-synthesized audio through the real recognizer. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Sherpa/ModelManagementSubsystem/SherpaOnnxZipformerEnRecognitionModel.cs");
            testFiles = ("test/DemaConsulting.Speech.Sherpa.Tests/ModelManagementSubsystem/SherpaOnnxZipformerEnRecognitionModelTests.cs");
            designFiles = ("docs/design/speech-sherpa/model-management-subsystem/sherpa-onnx-zipformer-en-recognition-model.md");
            verificationFiles = ("docs/verification/speech-sherpa/model-management-subsystem/sherpa-onnx-zipformer-en-recognition-model.md");
            requirementFiles = ("docs/reqstream/speech-sherpa/model-management-subsystem/sherpa-onnx-zipformer-en-recognition-model.yaml");
        }

        // Declares the contract and archive, wraps the sherpa-onnx recognizer, and restores casing in
        // transcripts.
        dependency SherpaOnnxZipformerEnRecognitionModel to SherpaOnnxRecognitionEngine;
        dependency SherpaOnnxZipformerEnRecognitionModel to TarBz2ArchiveExtractor;
        dependency SherpaOnnxZipformerEnRecognitionModel to UppercaseTranscriptRestorer;
        dependency SherpaOnnxZipformerEnRecognitionModel to Speech_AudioFormat::AudioFormat;
        dependency SherpaOnnxZipformerEnRecognitionModel to Speech_RecognitionBackend::RecognitionBackend;
        dependency SherpaOnnxZipformerEnRecognitionModel to Speech_SpeechModelContract::SpeechModelContract;
        dependency SherpaOnnxZipformerEnRecognitionModel to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency SherpaOnnxZipformerEnRecognitionModel to Speech_SpeechModelDownloadDescriptor::SpeechModelDownloadDescriptor;
        dependency SherpaOnnxZipformerEnRecognitionModel to Speech_SpeechModelDownloadFile::SpeechModelDownloadFile;
        dependency SherpaOnnxZipformerEnRecognitionModel to Speech_SpeechModelParameters::SpeechModelParameters;
        dependency SherpaOnnxZipformerEnRecognitionModel to OtsDependencies::SherpaOnnx;
    }
}
