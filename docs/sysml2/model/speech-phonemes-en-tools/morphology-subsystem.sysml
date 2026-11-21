package SpeechPhonemesEnTools_MorphologySubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEnTools_MeasureMorphologyCommand::*;
    private import SpeechPhonemesEnTools_MorphologyAudit::*;

    part def MorphologySubsystem {
        doc /* The measure-morphology command: measures the library's morphology rules against the embedded lexicon. */

        metadata info : SoftwareInfo {
        }

        part measureMorphologyCommand : MeasureMorphologyCommand;
        part morphologyAudit : MorphologyAudit;
    }
}
