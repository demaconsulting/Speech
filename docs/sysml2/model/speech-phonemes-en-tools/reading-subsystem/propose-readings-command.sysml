package SpeechPhonemesEnTools_ProposeReadingsCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEnTools_ReadingProposer::*;
    private import SpeechPhonemesEnTools_ToolPaths::*;

    part def ProposeReadingsCommand {
        doc /* The propose-readings command: proposes pronunciations for the reading ids of the text analysis package
             * for human review and, with --check, verifies the frozen table against the inventory. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Phonemes.En.Tools/ReadingSubsystem/ProposeReadingsCommand.cs");
        }

        // Runs ReadingProposer over CmuLexicon and checks the frozen ReadingPronunciations table.
        dependency ProposeReadingsCommand to ReadingProposer;
        dependency ProposeReadingsCommand to ToolPaths;
        dependency ProposeReadingsCommand to SpeechPhonemesEn_CmuLexicon::CmuLexicon;
        dependency ProposeReadingsCommand to SpeechPhonemesEn_ReadingPronunciations::ReadingPronunciations;
    }
}
