package SpeechProofreader_SessionSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechProofreader_FlagCategory::*;
    private import SpeechProofreader_ProofreadSession::*;
    private import SpeechProofreader_SessionFile::*;
    private import SpeechProofreader_TsvExporter::*;
    private import SpeechProofreader_UserSettings::*;

    part def SessionSubsystem {
        doc /* The proofreading session: flags and their categories, the session file, the TSV export and the
             * remembered user settings. */

        metadata info : SoftwareInfo {
        }

        part flagCategory : FlagCategory;
        part proofreadSession : ProofreadSession;
        part sessionFile : SessionFile;
        part tsvExporter : TsvExporter;
        part userSettings : UserSettings;
    }
}
