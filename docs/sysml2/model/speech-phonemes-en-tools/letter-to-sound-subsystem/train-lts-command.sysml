package SpeechPhonemesEnTools_TrainLtsCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEnTools_BenchmarkLtsCommand::*;
    private import SpeechPhonemesEnTools_DataManifest::*;
    private import SpeechPhonemesEnTools_LtsCorpus::*;
    private import SpeechPhonemesEnTools_LtsTrainer::*;
    private import SpeechPhonemesEnTools_ToolPaths::*;

    part def TrainLtsCommand {
        doc /* The train-lts command: trains the shipped letter-to-sound model from the whole pinned CMUdict and
             * writes the embedded resource or, with --check, verifies the committed resource is what the training
             * produces. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Phonemes.En.Tools/LetterToSoundSubsystem/TrainLtsCommand.cs");
        }

        // Trains on the pinned LTS corpus with LtsTrainer, then writes or checks the resource at ToolPaths
        // locations.
        dependency TrainLtsCommand to BenchmarkLtsCommand;
        dependency TrainLtsCommand to DataManifest;
        dependency TrainLtsCommand to LtsCorpus;
        dependency TrainLtsCommand to LtsTrainer;
        dependency TrainLtsCommand to ToolPaths;
    }
}
