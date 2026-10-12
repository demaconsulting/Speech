package SpeechCli {
    part def ICliModelCatalog {
        doc /* CLI-owned seam for enumerating known models, downloading, and asynchronously uninstalling and
             * cleaning up leftovers by id (UninstallAsync/CleanUpLeftoversAsync), so every command can be tested against controlled
             * catalog data with no network access. Also the single seam point extended by
             * SynthesisCommandSubsystem/RecognitionCommandSubsystem with async engine-loading
             * members (GetPreferredAudioFormat/CreateSynthesizerEngineAsync,
             * CreateRecognizerEngineAsync) returning the library's async
             * ISpeechSynthesizerEngine/ISpeechRecognizerEngine, each later bound to a device
             * via CreateSessionAsync rather than returning a device-bound synthesizer or
             * recognizer directly. Also exposes the reference-voice members (VoicePacks,
             * UserVoicesDirectory, EnumerateVoices, GetVoicePackState, InstallVoicePackAsync,
             * UninstallVoicePackAsync, VoicePackHasEmotionsAsync) used by list-voices and voices. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/ICliModelCatalog.cs */
    }
}
