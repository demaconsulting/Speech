package SpeechPhonemesEnTools_CmuDictSource {
    private import SoftwareItemMetadata::*;

    part def CmuDictSource {
        doc /* Reads the CMUdict source file (cmudict.dict) into the entries the lexicon image holds, rejecting
             * malformed lines. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Phonemes.En.Tools/LexiconSubsystem/CmuDictSource.cs");
        }

        // Parses cmudict.dict lines into CmuLexicon entries, validating each pronunciation against the
        // Phone set.
        dependency CmuDictSource to SpeechPhonemesEn_CmuLexicon::CmuLexicon;
        dependency CmuDictSource to SpeechPhonemesEn_Phone::Phone;
    }
}
