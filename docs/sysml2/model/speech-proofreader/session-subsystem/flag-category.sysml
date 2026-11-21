package SpeechProofreader_FlagCategory {
    private import SoftwareItemMetadata::*;

    enum def FlagCategoryKind {
        enum WrongHeteronym;
        enum MispronouncedWord;
        enum Normalization;
        enum MissingOrExtraWord;
        enum ProsodyOrPause;
        enum VoiceGlitch;
        enum Other;
    }

    part def FlagCategory {
        doc /* The kinds of problem a listener flags in a sentence, with their labels. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/Session/FlagCategory.cs");
        }
    }
}
