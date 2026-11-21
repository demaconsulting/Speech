package SpeechProofreader_TextSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechProofreader_MarkdownStripper::*;
    private import SpeechProofreader_ProofreadSentence::*;
    private import SpeechProofreader_SentenceSplitter::*;

    part def TextSubsystem {
        doc /* The text being proofread: Markdown stripping and sentence splitting with the same English analysis the
             * models use. */

        metadata info : SoftwareInfo {
        }

        part markdownStripper : MarkdownStripper;
        part sentenceSplitter : SentenceSplitter;
        part proofreadSentence : ProofreadSentence;
    }
}
