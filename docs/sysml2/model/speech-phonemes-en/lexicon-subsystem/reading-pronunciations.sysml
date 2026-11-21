package SpeechPhonemesEn_ReadingPronunciations {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEn_Morphology::*;
    private import SpeechPhonemesEn_Phone::*;

    part def ReadingPronunciations {
        doc /* Internal frozen, reviewed table that maps every reading id of the English text analysis inventory to
             * the pronunciation of its headword (from a dictionary variant, a named rule or authored), with the few
             * listed spellings that the sound rules cannot give, and resolves a spelling under a reading to the
             * headword, a regular inflection of it or an override, reporting everything else as an explicit gap. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Phonemes.En/LexiconSubsystem/ReadingPronunciations.cs", "src/DemaConsulting.Speech.Phonemes.En/LexiconSubsystem/LexiconCorrections.cs");
            testFiles = ("test/DemaConsulting.Speech.Phonemes.En.Tests/LexiconSubsystemTests.Readings.cs");
            designFiles = ("docs/design/speech-phonemes-en/lexicon-subsystem/reading-pronunciations.md");
            verificationFiles = ("docs/verification/speech-phonemes-en/lexicon-subsystem/reading-pronunciations.md");
            requirementFiles = ("docs/reqstream/speech-phonemes-en/lexicon-subsystem/reading-pronunciations.yaml");
        }

        // Maps text-analysis reading ids to phone pronunciations, using morphology for inflected forms.
        dependency ReadingPronunciations to Morphology;
        dependency ReadingPronunciations to Phone;
    }
}
