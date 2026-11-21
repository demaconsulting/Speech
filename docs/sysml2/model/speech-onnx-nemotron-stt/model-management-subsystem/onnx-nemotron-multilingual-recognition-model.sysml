package SpeechOnnxNemotronStt_OnnxNemotronMultilingualRecognitionModel {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxNemotronStt_NemotronEncoder::*;
    private import SpeechOnnxNemotronStt_NemotronRnntGreedyDecoder::*;
    private import SpeechOnnxNemotronStt_NemotronRnntNetwork::*;
    private import SpeechOnnxNemotronStt_NemotronVocabulary::*;
    private import SpeechOnnxNemotronStt_OnnxNemotronRecognitionEngine::*;

    part def OnnxNemotronMultilingualRecognitionModel {
        doc /* Real IRecognitionModel for NVIDIA Nemotron 3.5 ASR streaming 0.6B (int4 ONNX export). One catalog id
             * with a language ChoiceParameter (en-US, en-GB); declares the eleven-file checksum-verified download
             * descriptor; CreateBackend builds the encoder session through OnnxExecutionProviderSelector and the
             * decoder and joint CPU sessions, and constructs the OnnxNemotronRecognitionEngine. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.NemotronStt/ModelManagementSubsystem/OnnxNemotronMultilingualRecognitionModel.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/ModelManagementSubsystem/OnnxNemotronMultilingualRecognitionModelTests.cs");
            designFiles = ("docs/design/speech-onnx-nemotron-stt/model-management-subsystem/onnx-nemotron-multilingual-recognition-model.md");
            verificationFiles = ("docs/verification/speech-onnx-nemotron-stt/model-management-subsystem/onnx-nemotron-multilingual-recognition-model.md");
            requirementFiles = ("docs/reqstream/speech-onnx-nemotron-stt/model-management-subsystem/onnx-nemotron-multilingual-recognition-model.yaml");
        }

        // Declares the model contract and downloads, then builds the Nemotron engine, decoder, and
        // vocabulary.
        dependency OnnxNemotronMultilingualRecognitionModel to NemotronEncoder;
        dependency OnnxNemotronMultilingualRecognitionModel to NemotronRnntGreedyDecoder;
        dependency OnnxNemotronMultilingualRecognitionModel to NemotronRnntNetwork;
        dependency OnnxNemotronMultilingualRecognitionModel to NemotronVocabulary;
        dependency OnnxNemotronMultilingualRecognitionModel to OnnxNemotronRecognitionEngine;
        dependency OnnxNemotronMultilingualRecognitionModel to SpeechOnnx_OnnxExecutionProviderSelector::OnnxExecutionProviderSelector;
        dependency OnnxNemotronMultilingualRecognitionModel to Speech_AudioFormat::AudioFormat;
        dependency OnnxNemotronMultilingualRecognitionModel to Speech_RecognitionBackend::RecognitionBackend;
        dependency OnnxNemotronMultilingualRecognitionModel to Speech_SpeechModelContract::SpeechModelContract;
        dependency OnnxNemotronMultilingualRecognitionModel to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency OnnxNemotronMultilingualRecognitionModel to Speech_SpeechModelDownloadDescriptor::SpeechModelDownloadDescriptor;
        dependency OnnxNemotronMultilingualRecognitionModel to Speech_SpeechModelDownloadFile::SpeechModelDownloadFile;
        dependency OnnxNemotronMultilingualRecognitionModel to Speech_SpeechModelParameters::SpeechModelParameters;
        dependency OnnxNemotronMultilingualRecognitionModel to OtsDependencies::MicrosoftMlOnnxRuntime;
    }
}
