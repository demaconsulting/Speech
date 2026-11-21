package SpeechPhonemesEnTools {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEnTools_LetterToSoundSubsystem::*;
    private import SpeechPhonemesEnTools_LexiconSubsystem::*;
    private import SpeechPhonemesEnTools_MorphologySubsystem::*;
    private import SpeechPhonemesEnTools_PhonemizerSubsystem::*;
    private import SpeechPhonemesEnTools_Program::*;
    private import SpeechPhonemesEnTools_ReadingSubsystem::*;
    private import SpeechPhonemesEnTools_RenderingSubsystem::*;
    private import SpeechPhonemesEnTools_ToolPaths::*;

    doc
    /*
     * SpeechPhonemesEnTools is the non-shipped DemaConsulting.Speech.Phonemes.En.Tools console tool that builds and
     * checks the embedded data of the SpeechPhonemesEn package (the lexicon image, the letter-to-sound model and the
     * reading table), measures the library and regenerates the golden corpora. It is reviewed in the
     * SpeechPhonemesEn-DataTools review sets and is never packaged.
     */

    part def SpeechPhonemesEnToolsSystem {
        doc /* The non-shipped Phonemes.En tools console application as a whole (described in the data provenance
             * sections of the SpeechPhonemesEn design; it has no design, verification or requirements documents of
             * its own). */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech-phonemes-en.md");
        }

        part program : Program;
        part toolPaths : ToolPaths;
        part lexiconSubsystem : LexiconSubsystem;
        part letterToSoundSubsystem : LetterToSoundSubsystem;
        part morphologySubsystem : MorphologySubsystem;
        part phonemizerSubsystem : PhonemizerSubsystem;
        part readingSubsystem : ReadingSubsystem;
        part renderingSubsystem : RenderingSubsystem;
    }
}
