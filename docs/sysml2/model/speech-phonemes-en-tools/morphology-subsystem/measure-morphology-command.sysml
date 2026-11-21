package SpeechPhonemesEnTools_MeasureMorphologyCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEnTools_MorphologyAudit::*;

    part def MeasureMorphologyCommand {
        doc /* The measure-morphology command: measures the morphology rules on the embedded lexicon and prints the
             * agreement per ending with the disagreement classes. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Phonemes.En.Tools/MorphologySubsystem/MeasureMorphologyCommand.cs");
        }

        // Runs MorphologyAudit over the embedded CmuLexicon and the Morphology rules, then prints the
        // results.
        dependency MeasureMorphologyCommand to MorphologyAudit;
        dependency MeasureMorphologyCommand to SpeechPhonemesEn_CmuLexicon::CmuLexicon;
        dependency MeasureMorphologyCommand to SpeechPhonemesEn_Morphology::Morphology;
    }
}
