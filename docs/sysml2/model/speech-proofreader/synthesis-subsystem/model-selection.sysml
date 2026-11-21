package SpeechProofreader_ModelSelection {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    part def ModelSelection {
        doc /* A synthesis model plus the parameter values it is loaded with: the identity of one audible
             * configuration. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/Synthesis/ModelSelection.cs");
        }

        attribute modelId : String;
        attribute displayName : String;
        // Parameters: one entry per key; parameters_key[i] maps to parameters_value[i].
        attribute parameters_key : String [0..*];
        attribute parameters_value : ScalarValue [0..*];
        attribute key : String;
    }
}
