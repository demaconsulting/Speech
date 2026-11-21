package SpeechProofreader_ProofreadSession {
    private import SoftwareItemMetadata::*;
    private import SpeechProofreader_FlagCategory::*;
    private import SpeechProofreader_ModelSelection::*;

    part def ProofreadSession {
        doc /* The serializable session: the text, the model selections with their parameter values and the flags. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/Session/ProofreadSession.cs");
        }

        // Stores the ModelSelection settings and FlagCategory-tagged flags that make up a session.
        dependency ProofreadSession to FlagCategory;
        dependency ProofreadSession to ModelSelection;
    }
}
