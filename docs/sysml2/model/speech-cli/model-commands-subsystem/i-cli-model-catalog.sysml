package SpeechCli_ICliModelCatalog {
    private import SoftwareItemMetadata::*;

    part def ICliModelCatalog {
        doc /* CLI-owned seam for enumerating known models, downloading, and asynchronously uninstalling and cleaning
             * up leftovers by id (UninstallAsync/CleanUpLeftoversAsync), so every command can be tested against
             * controlled catalog data with no network access. Also the single seam point extended by
             * SynthesisCommandSubsystem/RecognitionCommandSubsystem with async engine-loading members
             * (GetPreferredAudioFormat/CreateSynthesizerEngineAsync, CreateRecognizerEngineAsync) returning the
             * library's async ISpeechSynthesizerEngine/ISpeechRecognizerEngine, each later bound to a device via
             * CreateSessionAsync rather than returning a device-bound synthesizer or recognizer directly. Also
             * exposes the reference-voice members (VoicePacks, UserVoicesDirectory, EnumerateVoices,
             * GetVoicePackState, InstallVoicePackAsync, UninstallVoicePackAsync, VoicePackHasEmotionsAsync) used by
             * list-voices and voices. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/ICliModelCatalog.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/SpeechModelCatalogAdapterTests.cs");
            designFiles = ("docs/design/speech-cli/model-commands-subsystem/i-cli-model-catalog.md");
            verificationFiles = ("docs/verification/speech-cli/model-commands-subsystem/i-cli-model-catalog.md");
            requirementFiles = ("docs/reqstream/speech-cli/model-commands-subsystem/i-cli-model-catalog.yaml");
        }

        // Hides the library catalog behind descriptors, engines, voice packs and downloader progress
        // types.
        dependency ICliModelCatalog to Speech_AudioFormat::AudioFormat;
        dependency ICliModelCatalog to Speech_ISpeechRecognizerEngine::ISpeechRecognizerEngine;
        dependency ICliModelCatalog to Speech_ISpeechSynthesizerEngine::ISpeechSynthesizerEngine;
        dependency ICliModelCatalog to Speech_ReferenceVoicePack::ReferenceVoicePack;
        dependency ICliModelCatalog to Speech_SpeechModelDescriptor::SpeechModelDescriptor;
        dependency ICliModelCatalog to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency ICliModelCatalog to Speech_SpeechModelDownloadProgress::SpeechModelDownloadProgress;
        dependency ICliModelCatalog to Speech_SpeechModelDownloader::SpeechModelDownloader;
    }
}
