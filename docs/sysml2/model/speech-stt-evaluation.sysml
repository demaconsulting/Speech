package SpeechSttEvaluation {
    private import SoftwareItemMetadata::*;
    private import SpeechSttEvaluation_EngineOptionOverrides::*;
    private import SpeechSttEvaluation_EvaluationReport::*;
    private import SpeechSttEvaluation_EvaluationRunner::*;
    private import SpeechSttEvaluation_Program::*;
    private import SpeechSttEvaluation_SttDataSet::*;
    private import SpeechSttEvaluation_SttEvaluationGates::*;
    private import SpeechSttEvaluation_SttTranscriber::*;
    private import SpeechSttEvaluation_ToolPaths::*;
    private import SpeechSttEvaluation_WordErrorRateCalculator::*;

    doc
    /*
     * SpeechSttEvaluation is the non-shipped DemaConsulting.Speech.SttEvaluation console tool that measures installed
     * speech-to-text models on the committed evaluation data in data/stt-en: word error rate, segmentation, invented
     * words, latency and speed. It gates the ONNX Nemotron model of SpeechOnnxNemotronStt (--check), compares it with
     * other installed models on identical audio (--compare, local only), and sweeps internal engine options
     * (--engine-option). It writes only below the git-ignored artifacts folder and is never packed.
     */

    part def SpeechSttEvaluationSystem {
        doc /* The non-shipped speech-to-text evaluation tool as a whole (described in the Evaluation Tool and Test
             * Data section of the SpeechOnnxNemotronStt design; it has no verification or requirements documents of
             * its own, and its shared sources are covered by the Evaluation tests of the SpeechOnnxNemotronStt test
             * project). */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech-onnx-nemotron-stt.md");
        }

        part program : Program;
        part toolPaths : ToolPaths;
        part evaluationRunner : EvaluationRunner;
        part evaluationReport : EvaluationReport;
        part engineOptionOverrides : EngineOptionOverrides;
        part wordErrorRateCalculator : WordErrorRateCalculator;
        part sttDataSet : SttDataSet;
        part sttTranscriber : SttTranscriber;
        part sttEvaluationGates : SttEvaluationGates;
    }
}