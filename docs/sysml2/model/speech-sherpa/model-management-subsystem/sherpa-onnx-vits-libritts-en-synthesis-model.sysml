package SpeechSherpa_SherpaOnnxVitsLibriTtsEnglishSynthesisModel {
    private import SoftwareItemMetadata::*;
    private import SpeechSherpa_SherpaOnnxSynthesisEngine::*;
    private import SpeechSherpa_TarBz2ArchiveExtractor::*;

    part def SherpaOnnxVitsLibriTtsEnglishSynthesisModel {
        doc /* Real, concrete ISynthesisModel backing sherpa-onnx's VITS/Piper text-to-speech voice
             * vits-piper-en_US-libritts_r-medium (CC BY 4.0, confirmed directly from this model's own MODEL_CARD).
             * Declares a single .tar.bz2 download, delegates InstallAsync to the shared TarBz2ArchiveExtractor,
             * builds the proven VITS offline text-to-speech configuration for this model, and owns a plain numeric
             * speaker-index NumericParameter (0-903) plus a matching ISynthesisModel.ResolveSpeakerId override, since
             * LibriTTS-R's 904 speaker embeddings have no published human-readable name mapping - closing the gap
             * previously deferred as a known, accepted, out-of-scope limitation. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Sherpa/ModelManagementSubsystem/SherpaOnnxVitsLibriTtsEnglishSynthesisModel.cs");
            testFiles = ("test/DemaConsulting.Speech.Sherpa.Tests/ModelManagementSubsystem/SherpaOnnxVitsLibriTtsEnglishSynthesisModelTests.cs");
            designFiles = ("docs/design/speech-sherpa/model-management-subsystem/sherpa-onnx-vits-libritts-en-synthesis-model.md");
            verificationFiles = ("docs/verification/speech-sherpa/model-management-subsystem/sherpa-onnx-vits-libritts-en-synthesis-model.md");
            requirementFiles = ("docs/reqstream/speech-sherpa/model-management-subsystem/sherpa-onnx-vits-libritts-en-synthesis-model.yaml");
        }

        // Declares the contract, bounds, and archive, then wraps sherpa-onnx VITS in the synthesis engine.
        dependency SherpaOnnxVitsLibriTtsEnglishSynthesisModel to SherpaOnnxSynthesisEngine;
        dependency SherpaOnnxVitsLibriTtsEnglishSynthesisModel to TarBz2ArchiveExtractor;
        dependency SherpaOnnxVitsLibriTtsEnglishSynthesisModel to Speech_AudioFormat::AudioFormat;
        dependency SherpaOnnxVitsLibriTtsEnglishSynthesisModel to Speech_NumericParameterBounds::NumericParameterBounds;
        dependency SherpaOnnxVitsLibriTtsEnglishSynthesisModel to Speech_SpeechModelContract::SpeechModelContract;
        dependency SherpaOnnxVitsLibriTtsEnglishSynthesisModel to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency SherpaOnnxVitsLibriTtsEnglishSynthesisModel to Speech_SpeechModelDownloadDescriptor::SpeechModelDownloadDescriptor;
        dependency SherpaOnnxVitsLibriTtsEnglishSynthesisModel to Speech_SpeechModelDownloadFile::SpeechModelDownloadFile;
        dependency SherpaOnnxVitsLibriTtsEnglishSynthesisModel to Speech_SpeechModelParameters::SpeechModelParameters;
        dependency SherpaOnnxVitsLibriTtsEnglishSynthesisModel to Speech_SynthesisBackend::SynthesisBackend;
        dependency SherpaOnnxVitsLibriTtsEnglishSynthesisModel to OtsDependencies::SherpaOnnx;
    }
}
