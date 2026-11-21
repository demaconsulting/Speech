package SpeechTextEn_HeteronymEvaluator {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_AveragedPerceptronTrainer::*;
    private import SpeechTextEn_CheckReport::*;
    private import SpeechTextEn_DeterministicRandom::*;
    private import SpeechTextEn_EnglishTextAnalyzer::*;
    private import SpeechTextEn_EnglishTextAnalyzerOptions::*;
    private import SpeechTextEn_EvaluationOverlay::*;
    private import SpeechTextEn_HeteronymClassifier::*;
    private import SpeechTextEn_HeteronymFeatureExtractor::*;
    private import SpeechTextEn_PartOfSpeechAdapter::*;
    private import SpeechTextEn_SparsePerceptronModel::*;
    private import SpeechTextEn_TextEnAnnotations::*;
    private import SpeechTextEn_TextEnDataSet::*;
    private import SpeechTextEn_TrainingCorpus::*;
    private import SpeechTextEn_TrainingRecord::*;
    private import SpeechTextEn_TsvTable::*;

    part def HeteronymEvaluator {
        doc /* End-to-end model evaluator: decides each row through the real EnglishTextAnalyzer (no reading counts as
             * wrong), compares it with the direct classifier path for parity, reports three baselines and strata by
             * source, reading and confidence, computes the paired document-clustered bootstrap interval in integer
             * arithmetic, decides the statistical gate per scored set by an exact integer threshold comparison,
             * derives a threshold from seed counts (the floor of the mean minus two sample standard deviations), and
             * classifies flips as fixed, broken or changed. Also evaluates caller-supplied rows with caller-supplied
             * clustering (the external sets and the overlays) and reports a clustered interval of the accuracy itself
             * for them and for a gate set. Also holds the SetEvaluation, RowReading, GroupTally, BootstrapInterval,
             * ClusterStats, ParityCounts, SeedSetCount, SeedResult, GateSetVerdict and GateVerdict result records. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/TrainingSubsystem/HeteronymEvaluator.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/HeteronymEvaluatorTests.cs");
            designFiles = ("docs/design/speech-text-en/training-subsystem/heteronym-evaluator.md");
            verificationFiles = ("docs/verification/speech-text-en/training-subsystem/heteronym-evaluator.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-subsystem/heteronym-evaluator.yaml");
        }

        // Runs data rows through the real analyzer and classifier with the model and scores accuracy,
        // overlays and seeds.
        dependency HeteronymEvaluator to AveragedPerceptronTrainer;
        dependency HeteronymEvaluator to CheckReport;
        dependency HeteronymEvaluator to DeterministicRandom;
        dependency HeteronymEvaluator to EnglishTextAnalyzer;
        dependency HeteronymEvaluator to EnglishTextAnalyzerOptions;
        dependency HeteronymEvaluator to EvaluationOverlay;
        dependency HeteronymEvaluator to HeteronymClassifier;
        dependency HeteronymEvaluator to HeteronymFeatureExtractor;
        dependency HeteronymEvaluator to PartOfSpeechAdapter;
        dependency HeteronymEvaluator to SparsePerceptronModel;
        dependency HeteronymEvaluator to TextEnAnnotations;
        dependency HeteronymEvaluator to TextEnDataSet;
        dependency HeteronymEvaluator to TrainingCorpus;
        dependency HeteronymEvaluator to TrainingRecord;
        dependency HeteronymEvaluator to TsvTable;
    }
}
