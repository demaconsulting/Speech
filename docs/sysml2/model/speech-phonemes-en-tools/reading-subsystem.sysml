package SpeechPhonemesEnTools_ReadingSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEnTools_ProposeReadingsCommand::*;
    private import SpeechPhonemesEnTools_ReadingProposer::*;

    part def ReadingSubsystem {
        doc /* The propose-readings command: proposes pronunciations for the reading ids of the Text.En inventory for
             * human review and verifies the frozen reading table against the inventory. */

        metadata info : SoftwareInfo {
        }

        part proposeReadingsCommand : ProposeReadingsCommand;
        part readingProposer : ReadingProposer;
    }
}
