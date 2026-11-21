package SpeechPhonemesEnTools_Program {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEnTools_BenchmarkLtsCommand::*;
    private import SpeechPhonemesEnTools_BuildLexiconCommand::*;
    private import SpeechPhonemesEnTools_MeasureMorphologyCommand::*;
    private import SpeechPhonemesEnTools_MeasureOracleCommand::*;
    private import SpeechPhonemesEnTools_ProposeReadingsCommand::*;
    private import SpeechPhonemesEnTools_RegenerateGoldensCommand::*;
    private import SpeechPhonemesEnTools_TrainLtsCommand::*;

    part def Program {
        doc /* Entry point of the tools command line: dispatches build-lexicon, train-lts and propose-readings (the
             * reproducibility gates, each with --check), regenerate-goldens and the measurements measure-morphology,
             * benchmark-lts and measure-oracle to their command units. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Phonemes.En.Tools/Program.cs");
        }

        // Routes each tools subcommand to the command unit that builds, checks, regenerates, or measures
        // it.
        dependency Program to BenchmarkLtsCommand;
        dependency Program to BuildLexiconCommand;
        dependency Program to MeasureMorphologyCommand;
        dependency Program to MeasureOracleCommand;
        dependency Program to ProposeReadingsCommand;
        dependency Program to RegenerateGoldensCommand;
        dependency Program to TrainLtsCommand;
    }
}
