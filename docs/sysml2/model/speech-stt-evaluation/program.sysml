package SpeechSttEvaluation_Program {
    private import SoftwareItemMetadata::*;
    private import SpeechSttEvaluation_EngineOptionOverrides::*;
    private import SpeechSttEvaluation_EvaluationReport::*;
    private import SpeechSttEvaluation_EvaluationRunner::*;
    private import SpeechSttEvaluation_SttDataSet::*;
    private import SpeechSttEvaluation_SttEvaluationGates::*;
    private import SpeechSttEvaluation_ToolPaths::*;

    part def Program {
        doc /* Entry point of the evaluation tool: parses the evaluate command (--model, --compare, --check,
             * --scenario, --engine-option, --trailing-silence, --data, --models-dir, --output), resolves the known
             * recognition models (the ONNX Nemotron model and the Sherpa Nemotron and Zipformer models) against the
             * model store, runs the evaluation, writes the reports, and applies the accuracy gates. Exit codes: 0
             * success, 1 a gate failed, 2 a usage, data or model error. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.SttEvaluation/Program.cs");
        }

        // Dispatches the evaluate command to the runner, the reports and the gates, and builds the models it can
        // evaluate from the library packages and the model store.
        dependency Program to EngineOptionOverrides;
        dependency Program to EvaluationReport;
        dependency Program to EvaluationRunner;
        dependency Program to SttDataSet;
        dependency Program to SttEvaluationGates;
        dependency Program to ToolPaths;
        dependency Program to SpeechOnnxNemotronStt_OnnxNemotronMultilingualRecognitionModel::OnnxNemotronMultilingualRecognitionModel;
        dependency Program to SpeechSherpa_SherpaOnnxNemotronStreamingEnRecognitionModel::SherpaOnnxNemotronStreamingEnRecognitionModel;
        dependency Program to SpeechSherpa_SherpaOnnxZipformerEnRecognitionModel::SherpaOnnxZipformerEnRecognitionModel;
        dependency Program to Speech_SpeechModelContract::SpeechModelContract;
        dependency Program to Speech_SpeechModelStore::SpeechModelStore;
        dependency Program to Speech_SpeechModelStoreOptions::SpeechModelStoreOptions;
    }
}