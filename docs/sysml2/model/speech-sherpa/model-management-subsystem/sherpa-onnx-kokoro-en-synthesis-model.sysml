package SpeechSherpa_SherpaOnnxKokoroEnglishSynthesisModel {
    private import SoftwareItemMetadata::*;
    private import SpeechSherpa_SherpaOnnxSynthesisEngine::*;
    private import SpeechSherpa_TarBz2ArchiveExtractor::*;

    part def SherpaOnnxKokoroEnglishSynthesisModel {
        doc /* Real, concrete ISynthesisModel backing sherpa-onnx's Kokoro text-to-speech archive kokoro-int8-en-v0_19
             * (Apache-2.0, confirmed directly from the archive's own LICENSE file), an English-only, int8-quantized,
             * StyleTTS2-derived multi-speaker model. Declares a single .tar.bz2 download, delegates InstallAsync to
             * the shared TarBz2ArchiveExtractor, builds the proven Kokoro offline text-to-speech configuration for
             * this model, and owns a confirmed voice-name-to-speaker-id mapping via ISynthesisModel.ResolveSpeakerId,
             * closing the "speaker selection is out of scope" gap the sibling
             * SherpaOnnxVitsLibriTtsEnglishSynthesisModel explicitly deferred. Declares 11 genuinely distinct voices
             * as a ChoiceParameter, confirmed against upstream sherpa-onnx's own generate_voices_bin.py script, byte
             * arithmetic against the real voices.bin, and a live loaded model's NumSpeakers. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Sherpa/ModelManagementSubsystem/SherpaOnnxKokoroEnglishSynthesisModel.cs");
            testFiles = ("test/DemaConsulting.Speech.Sherpa.Tests/ModelManagementSubsystem/SherpaOnnxKokoroEnglishSynthesisModelTests.cs");
            designFiles = ("docs/design/speech-sherpa/model-management-subsystem/sherpa-onnx-kokoro-en-synthesis-model.md");
            verificationFiles = ("docs/verification/speech-sherpa/model-management-subsystem/sherpa-onnx-kokoro-en-synthesis-model.md");
            requirementFiles = ("docs/reqstream/speech-sherpa/model-management-subsystem/sherpa-onnx-kokoro-en-synthesis-model.yaml");
        }

        // Declares the contract and download archive, extracts it, and wraps sherpa-onnx in the synthesis
        // engine.
        dependency SherpaOnnxKokoroEnglishSynthesisModel to SherpaOnnxSynthesisEngine;
        dependency SherpaOnnxKokoroEnglishSynthesisModel to TarBz2ArchiveExtractor;
        dependency SherpaOnnxKokoroEnglishSynthesisModel to Speech_AudioFormat::AudioFormat;
        dependency SherpaOnnxKokoroEnglishSynthesisModel to Speech_SpeechModelContract::SpeechModelContract;
        dependency SherpaOnnxKokoroEnglishSynthesisModel to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency SherpaOnnxKokoroEnglishSynthesisModel to Speech_SpeechModelDownloadDescriptor::SpeechModelDownloadDescriptor;
        dependency SherpaOnnxKokoroEnglishSynthesisModel to Speech_SpeechModelDownloadFile::SpeechModelDownloadFile;
        dependency SherpaOnnxKokoroEnglishSynthesisModel to Speech_SpeechModelParameters::SpeechModelParameters;
        dependency SherpaOnnxKokoroEnglishSynthesisModel to Speech_SynthesisBackend::SynthesisBackend;
        dependency SherpaOnnxKokoroEnglishSynthesisModel to OtsDependencies::SherpaOnnx;
    }
}
