package SpeechTextEn_EvaluateExternalCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_CheckDataCommand::*;
    private import SpeechTextEn_EvaluateCommand::*;
    private import SpeechTextEn_EvaluationRecord::*;
    private import SpeechTextEn_ExternalEvaluationRecord::*;
    private import SpeechTextEn_ExternalRowConversion::*;
    private import SpeechTextEn_ExternalSourceCache::*;
    private import SpeechTextEn_HeteronymBenchAdapter::*;
    private import SpeechTextEn_HeteronymEvaluator::*;
    private import SpeechTextEn_LeakageChecker::*;
    private import SpeechTextEn_ModelSelection::*;
    private import SpeechTextEn_SparsePerceptronModel::*;
    private import SpeechTextEn_TrainCommand::*;
    private import SpeechTextEn_TrainingCorpus::*;
    private import SpeechTextEn_TrainingRecord::*;
    private import SpeechTextEn_WikipediaHomographAdapter::*;

    part def EvaluateExternalCommand {
        doc /* Command line entry of the trainer's evaluate-external command: guards the cache location, fetches only
             * on request, verifies the pinned sources, converts and evaluates the requested external sets, runs the
             * seed study from the training and development data alone, prints only counts and our own identifiers,
             * and writes or verifies the external evaluation record. Exit codes: 0 done or record current, 1 record
             * stale, 2 usage, input, pin, hash or data error (with no figures printed). The Program.cs entry point
             * only dispatches to this unit. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/ExternalEvaluationSubsystem/EvaluateExternalCommand.cs", "tools/DemaConsulting.Speech.Text.En.Trainer/Program.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/ExternalEvaluationSubsystem/EvaluateExternalCommandTests.cs");
            designFiles = ("docs/design/speech-text-en/external-evaluation-subsystem/evaluate-external-command.md");
            verificationFiles = ("docs/verification/speech-text-en/external-evaluation-subsystem/evaluate-external-command.md");
            requirementFiles = ("docs/reqstream/speech-text-en/external-evaluation-subsystem/evaluate-external-command.yaml");
        }

        // Fetches and converts external datasets via the adapters and cache, then evaluates the model and
        // writes records.
        dependency EvaluateExternalCommand to CheckDataCommand;
        dependency EvaluateExternalCommand to EvaluateCommand;
        dependency EvaluateExternalCommand to EvaluationRecord;
        dependency EvaluateExternalCommand to ExternalEvaluationRecord;
        dependency EvaluateExternalCommand to ExternalRowConversion;
        dependency EvaluateExternalCommand to ExternalSourceCache;
        dependency EvaluateExternalCommand to HeteronymBenchAdapter;
        dependency EvaluateExternalCommand to HeteronymEvaluator;
        dependency EvaluateExternalCommand to LeakageChecker;
        dependency EvaluateExternalCommand to ModelSelection;
        dependency EvaluateExternalCommand to SparsePerceptronModel;
        dependency EvaluateExternalCommand to TrainCommand;
        dependency EvaluateExternalCommand to TrainingCorpus;
        dependency EvaluateExternalCommand to TrainingRecord;
        dependency EvaluateExternalCommand to WikipediaHomographAdapter;
    }
}
