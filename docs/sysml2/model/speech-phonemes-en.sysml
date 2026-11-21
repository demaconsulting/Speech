package SpeechPhonemesEn {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEn_LetterToSoundSubsystem::*;
    private import SpeechPhonemesEn_LexiconSubsystem::*;
    private import SpeechPhonemesEn_PhonemizerSubsystem::*;
    private import SpeechPhonemesEn_RenderingSubsystem::*;

    doc
    /*
     * SpeechPhonemesEn is the DemaConsulting.Speech.Phonemes.En library that turns English text, with the annotations
     * of the SpeechTextEn text analysis (normalized and spelled replacements, heteronym readings, host pronunciation
     * overrides) or without them, into a stream of words with ARPAbet pronunciations and their provenance, and
     * renders that stream as the phoneme string of a voice model: espeak-style IPA for the Piper voice and misaki
     * symbols for Kokoro. Its only pronunciation source is the CMU Pronouncing Dictionary, embedded as a compact
     * image, plus the morphology rules over it, a reviewed table of the pronunciation of every reading id of the text
     * analysis, and a letter-to-sound model trained on the dictionary alone for words it does not know. The
     * Phonemizer subsystem depends on the Lexicon and LetterToSound subsystems, the Rendering subsystem on the result
     * types of the Phonemizer subsystem, and the LetterToSound subsystem on the Lexicon subsystem's phone types,
     * never the reverse. The library has no inference runtime and no NuGet dependency of its own; it uses the Speech
     * library's SegmentAnnotation and the SpeechTextEn annotation vocabulary and reading inventory. It is a preview
     * used by the Kokoro and Piper model packages. The non-shipped tools project
     * (tools/DemaConsulting.Speech.Phonemes.En.Tools) builds and checks the embedded data and regenerates the golden
     * corpora; it is not part of the package.
     */

    part def SpeechPhonemesEnSystem {
        doc /* The SpeechPhonemesEn library as a whole: English text to ARPAbet words to voice phoneme strings. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Phonemes.En.Tests/SpeechPhonemesEnTests.cs");
            designFiles = ("docs/design/speech-phonemes-en.md");
            verificationFiles = ("docs/verification/speech-phonemes-en.md");
            requirementFiles = ("docs/reqstream/speech-phonemes-en.yaml");
        }

        part lexiconSubsystem : LexiconSubsystem;
        part letterToSoundSubsystem : LetterToSoundSubsystem;
        part phonemizerSubsystem : PhonemizerSubsystem;
        part renderingSubsystem : RenderingSubsystem;
    }
}
