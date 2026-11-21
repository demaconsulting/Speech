package SpeechPhonemesEn_CmuLexicon {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEn_Phone::*;

    part def CmuLexicon {
        doc /* Internal lexicon of the CMU Pronouncing Dictionary: a compact front-coded image that is embedded in the
             * package, searched by binary search over block heads, and answers for a word (any case, straight or
             * typographic apostrophe) all its pronunciation variants in dictionary order, and none for an absent or
             * invalid word. Includes the documented image format shared with the non-shipped tools project. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Phonemes.En/LexiconSubsystem/CmuLexicon.cs", "src/DemaConsulting.Speech.Phonemes.En/LexiconSubsystem/LexiconImageFormat.cs");
            testFiles = ("test/DemaConsulting.Speech.Phonemes.En.Tests/LexiconSubsystemTests.cs", "test/DemaConsulting.Speech.Phonemes.En.Tests/LexiconSubsystemTests.Images.cs");
            designFiles = ("docs/design/speech-phonemes-en/lexicon-subsystem/cmu-lexicon.md");
            verificationFiles = ("docs/verification/speech-phonemes-en/lexicon-subsystem/cmu-lexicon.md");
            requirementFiles = ("docs/reqstream/speech-phonemes-en/lexicon-subsystem/cmu-lexicon.yaml");
        }

        // Stores CMU dictionary pronunciations as phone sequences.
        dependency CmuLexicon to Phone;
    }
}
