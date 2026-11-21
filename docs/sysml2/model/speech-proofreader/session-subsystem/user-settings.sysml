package SpeechProofreader_UserSettings {
    private import SoftwareItemMetadata::*;
    private import SpeechProofreader_FlagCategory::*;
    private import SpeechProofreader_ProofreadSession::*;
    private import SpeechProofreader_SessionFile::*;
    private import ScalarValues::*;

    part def UserSettings {
        doc /* What the Proofreader remembers between launches (the models and their settings, A/B mode and the last
             * flag category) and its per-user store. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/Session/UserSettings.cs");
            testFiles = ("test/DemaConsulting.Speech.Proofreader.Tests/UserSettingsTests.cs");
        }

        attribute abMode : Boolean;
        attribute lastCategory : SpeechProofreader_FlagCategory::FlagCategoryKind [0..1];

        // Saves session model selections and the last FlagCategory using SessionFile-style JSON
        // persistence.
        dependency UserSettings to FlagCategory;
        dependency UserSettings to ProofreadSession;
        dependency UserSettings to SessionFile;
    }
}
