package SpeechPhonemesEnTools_BenchmarkLtsCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEnTools_LtsBenchmark::*;
    private import SpeechPhonemesEnTools_LtsCorpus::*;
    private import SpeechPhonemesEnTools_LtsTrainer::*;
    private import SpeechPhonemesEnTools_NrlRules::*;
    private import SpeechPhonemesEnTools_ToolPaths::*;

    part def BenchmarkLtsCommand {
        doc /* The benchmark-lts command: the reproducible out-of-vocabulary benchmark of the rules baseline, the
             * trained model variants and optionally an external system's predictions on the stem-family held-out
             * split. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Phonemes.En.Tools/LetterToSoundSubsystem/BenchmarkLtsCommand.cs");
        }

        // Scores NRL rules and LtsTrainer-built models on the LtsCorpus held-out split using LtsBenchmark.
        dependency BenchmarkLtsCommand to LtsBenchmark;
        dependency BenchmarkLtsCommand to LtsCorpus;
        dependency BenchmarkLtsCommand to LtsTrainer;
        dependency BenchmarkLtsCommand to NrlRules;
        dependency BenchmarkLtsCommand to ToolPaths;
        dependency BenchmarkLtsCommand to SpeechPhonemesEn_LtsModel::LtsModel;
        dependency BenchmarkLtsCommand to SpeechPhonemesEn_Phone::Phone;
    }
}
