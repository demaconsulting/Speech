package SpeechSttEvaluation_EvaluationRunner {
    private import SoftwareItemMetadata::*;
    private import SpeechSttEvaluation_EngineOptionOverrides::*;
    private import SpeechSttEvaluation_EvaluationReport::*;
    private import SpeechSttEvaluation_SttDataSet::*;
    private import SpeechSttEvaluation_SttTranscriber::*;
    private import SpeechSttEvaluation_WordErrorRateCalculator::*;

    part def EvaluationRunner {
        doc /* Builds the evaluation scenarios from the data set (librispeech, poem, paused with 1.5 s gaps,
             * continuous of at least 45 s, quiet attenuated by 20 dB; EvaluationScenarios) and measures one model on
             * them: creates the backend once with the engine-option overrides, streams every recording through it
             * after a reset, and aligns each transcript with its reference (also verbatim for punctuated
             * references). Also holds the scenario, item and model result records. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.SttEvaluation/EvaluationRunner.cs");
        }

        // Streams the data set's recordings through the backend the overrides create and scores each transcript.
        dependency EvaluationRunner to EngineOptionOverrides;
        dependency EvaluationRunner to EvaluationReport;
        dependency EvaluationRunner to SttDataSet;
        dependency EvaluationRunner to SttTranscriber;
        dependency EvaluationRunner to WordErrorRateCalculator;
        dependency EvaluationRunner to Speech_RecognitionBackend::RecognitionBackend;
        dependency EvaluationRunner to Speech_SpeechModelContract::SpeechModelContract;
    }
}