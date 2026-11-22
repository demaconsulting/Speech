package ModelDownloader_Program {
    private import SoftwareItemMetadata::*;

    part def Program {
        doc /* Hosts the CI-only model download command: builds the four sherpa-onnx models of the SpeechSherpa
             * package and the ONNX Nemotron speech-to-text model of the SpeechOnnxNemotronStt package, and
             * downloads each requested model id into the per-user model store with an extended transfer
             * timeout, one model at a time so one failure never prevents the later requests, so automated runs can
             * provision real speech models before the integration tests execute. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/ModelDownloader/Program.cs");
        }

        // Builds the sherpa-onnx models and the ONNX Nemotron model and fetches each into the SpeechModelStore
        // through the HTTP downloader.
        dependency Program to SpeechOnnxNemotronStt_OnnxNemotronMultilingualRecognitionModel::OnnxNemotronMultilingualRecognitionModel;
        dependency Program to SpeechSherpa_SherpaOnnxKokoroEnglishSynthesisModel::SherpaOnnxKokoroEnglishSynthesisModel;
        dependency Program to SpeechSherpa_SherpaOnnxNemotronStreamingEnRecognitionModel::SherpaOnnxNemotronStreamingEnRecognitionModel;
        dependency Program to SpeechSherpa_SherpaOnnxVitsLibriTtsEnglishSynthesisModel::SherpaOnnxVitsLibriTtsEnglishSynthesisModel;
        dependency Program to SpeechSherpa_SherpaOnnxZipformerEnRecognitionModel::SherpaOnnxZipformerEnRecognitionModel;
        dependency Program to Speech_HttpModelDownloadClient::HttpModelDownloadClient;
        dependency Program to Speech_SpeechModelContract::SpeechModelContract;
        dependency Program to Speech_SpeechModelDownloadProgress::SpeechModelDownloadProgress;
        dependency Program to Speech_SpeechModelDownloader::SpeechModelDownloader;
        dependency Program to Speech_SpeechModelStore::SpeechModelStore;
    }
}
