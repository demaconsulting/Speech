package SpeechCli {
    part def SpeakCommand {
        doc /* Implements speak: resolves text from --text/--file/stdin (mutually
             * exclusive), resolves and validates the requested synthesis model, validates
             * --tts-param values, strips audio tags when --no-tags is given, dispatches to
             * either a WAV file (--output-audio) or a real playback device
             * (--playback-device/default), loads an ISpeechSynthesizerEngine via
             * ICliModelCatalog.CreateSynthesizerEngineAsync, binds an ISynthesisSession to
             * the resolved device, awaits session.SpeakAsync, and disposes the session,
             * then the engine, then the playback device (await using in declaration
             * order, reversed) on every exit path. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/SynthesisCommandSubsystem/SpeakCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/SynthesisCommandSubsystem/SpeakCommandTests.cs */
    }
}
