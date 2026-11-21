package SpeechPhonemesEnTools_ToolPaths {
    private import SoftwareItemMetadata::*;

    part def ToolPaths {
        doc /* Locates the repository folders the tool reads and writes (the data folder, the git-ignored CMUdict
             * download, the package resources and the evaluation record). */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Phonemes.En.Tools/ToolPaths.cs");
        }
    }
}
