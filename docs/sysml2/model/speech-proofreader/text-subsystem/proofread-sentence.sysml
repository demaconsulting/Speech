package SpeechProofreader_ProofreadSentence {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    part def ProofreadSentence {
        doc /* One sentence of the proofread text, located in the source text. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/Text/ProofreadSentence.cs");
        }

        attribute index : Integer;
        attribute start : Integer;
        attribute length : Integer;
        attribute text : String;
    }
}
