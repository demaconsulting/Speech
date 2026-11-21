package SpeechProofreader_ListItems {
    private import SoftwareItemMetadata::*;
    private import SpeechProofreader_FlagCategory::*;
    private import SpeechProofreader_ProofreadSentence::*;
    private import SpeechProofreader_ProofreadSession::*;

    part def ListItems {
        doc /* Row view models of the window's lists: sentence rows, flag rows, flag-category choices and device
             * choices. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/ListItems.cs");
        }

        // Wraps ProofreadSentence, session flags, and FlagCategory as MVVM rows for the window lists.
        dependency ListItems to FlagCategory;
        dependency ListItems to ProofreadSentence;
        dependency ListItems to ProofreadSession;
        dependency ListItems to OtsDependencies::CommunityToolkitMvvm;
    }
}
