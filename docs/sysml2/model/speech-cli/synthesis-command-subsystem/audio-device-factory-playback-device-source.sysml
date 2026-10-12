package SpeechCli {
    part def AudioDeviceFactoryPlaybackDeviceSource {
        doc /* Production ICliPlaybackDeviceSource implementation, forwarding every call
             * unchanged to a real, composed AudioDeviceFactory - including its
             * hardware-detection behavior - so SpeakCommand's real playback-device
             * resolution is exactly what it was before this seam was introduced. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/SynthesisCommandSubsystem/AudioDeviceFactoryPlaybackDeviceSource.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/SynthesisCommandSubsystem/AudioDeviceFactoryPlaybackDeviceSourceTests.cs */
    }
}
