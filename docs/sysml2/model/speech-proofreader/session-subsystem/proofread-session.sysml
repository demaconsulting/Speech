package SpeechProofreader_ProofreadSession {
    private import SoftwareItemMetadata::*;
    private import SpeechProofreader_FlagCategory::*;
    private import SpeechProofreader_ModelSelection::*;
    private import ScalarValues::*;

    attribute def SessionModel {
        attribute modelId : String;
        attribute displayName : String;
        // Parameters: one entry per key; parameters_key[i] maps to parameters_value[i].
        attribute parameters_key : String [0..*];
        attribute parameters_value : ScalarValue [0..*];
    }

    attribute def ProofreadFlag {
        attribute id : Integer;
        attribute sentenceIndex : Integer;
        attribute sentenceText : String;
        attribute spanStart : Integer;
        attribute spanLength : Integer;
        attribute word : String;
        attribute category : SpeechProofreader_FlagCategory::FlagCategoryKind;
        attribute note : String;
        attribute slot : String;
        attribute model : SessionModel;
        attribute readings : String;
        attribute phonemes : String;
        attribute hasSpan : Boolean;
    }

    part def ProofreadSession {
        doc /* The serializable session: the text, the model selections with their parameter values and the flags. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/Session/ProofreadSession.cs");
        }

        attribute version : Integer;
        attribute sourceName : String;
        attribute sourceSha256 : String;
        attribute text : String;
        attribute currentSentence : Integer;
        attribute abMode : Boolean;
        attribute slotA : SessionModel [0..1];
        attribute slotB : SessionModel [0..1];
        attribute playbackDevice : String [0..1];
        attribute flags : ProofreadFlag [0..*];

        // Stores the ModelSelection settings and FlagCategory-tagged flags that make up a session.
        dependency ProofreadSession to FlagCategory;
        dependency ProofreadSession to ModelSelection;
    }
}
