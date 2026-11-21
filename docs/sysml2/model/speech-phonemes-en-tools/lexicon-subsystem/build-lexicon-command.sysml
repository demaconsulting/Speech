package SpeechPhonemesEnTools_BuildLexiconCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEnTools_CmuDictSource::*;
    private import SpeechPhonemesEnTools_DataManifest::*;
    private import SpeechPhonemesEnTools_LexiconImageWriter::*;
    private import SpeechPhonemesEnTools_ToolPaths::*;

    part def BuildLexiconCommand {
        doc /* The build-lexicon command: derives the embedded lexicon image from the pinned CMUdict and, with
             * --check, verifies that the committed resource is exactly what the build produces. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Phonemes.En.Tools/LexiconSubsystem/BuildLexiconCommand.cs");
        }

        // Reads pinned CMUdict via the source reader and manifest, writes the lexicon image, and checks it
        // against CmuLexicon.
        dependency BuildLexiconCommand to CmuDictSource;
        dependency BuildLexiconCommand to DataManifest;
        dependency BuildLexiconCommand to LexiconImageWriter;
        dependency BuildLexiconCommand to ToolPaths;
        dependency BuildLexiconCommand to SpeechPhonemesEn_CmuLexicon::CmuLexicon;
    }
}
