package SpeechPhonemesEnTools_LexiconSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEnTools_BuildLexiconCommand::*;
    private import SpeechPhonemesEnTools_CmuDictSource::*;
    private import SpeechPhonemesEnTools_DataManifest::*;
    private import SpeechPhonemesEnTools_LexiconImageWriter::*;

    part def LexiconSubsystem {
        doc /* The build-lexicon command and its helpers: reads the pinned CMU Pronouncing Dictionary, writes the
             * embedded lexicon image and verifies the committed image against the data manifest. */

        metadata info : SoftwareInfo {
        }

        part buildLexiconCommand : BuildLexiconCommand;
        part cmuDictSource : CmuDictSource;
        part dataManifest : DataManifest;
        part lexiconImageWriter : LexiconImageWriter;
    }
}
