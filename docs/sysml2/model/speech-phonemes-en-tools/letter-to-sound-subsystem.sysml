package SpeechPhonemesEnTools_LetterToSoundSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEnTools_BenchmarkLtsCommand::*;
    private import SpeechPhonemesEnTools_FloatNetwork::*;
    private import SpeechPhonemesEnTools_LtsAligner::*;
    private import SpeechPhonemesEnTools_LtsBenchmark::*;
    private import SpeechPhonemesEnTools_LtsCorpus::*;
    private import SpeechPhonemesEnTools_LtsTrainer::*;
    private import SpeechPhonemesEnTools_NrlRules::*;
    private import SpeechPhonemesEnTools_TrainLtsCommand::*;

    part def LetterToSoundSubsystem {
        doc /* The train-lts and benchmark-lts commands: the stem-family split of the pinned dictionary,
             * letter-to-phone alignment, the floating-point training of the three networks and their integer image,
             * and the reproducible out-of-vocabulary benchmark against the rules baseline. */

        metadata info : SoftwareInfo {
        }

        part trainLtsCommand : TrainLtsCommand;
        part benchmarkLtsCommand : BenchmarkLtsCommand;
        part ltsCorpus : LtsCorpus;
        part ltsAligner : LtsAligner;
        part ltsTrainer : LtsTrainer;
        part floatNetwork : FloatNetwork;
        part ltsBenchmark : LtsBenchmark;
        part nrlRules : NrlRules;
    }
}
