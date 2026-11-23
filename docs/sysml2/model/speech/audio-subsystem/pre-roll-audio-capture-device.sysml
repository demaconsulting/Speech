package Speech_PreRollAudioCaptureDevice {
    private import SoftwareItemMetadata::*;
    private import Speech_IAudioCaptureDevice::*;
    private import Speech_AudioDeviceUnavailableException::*;

    part def PreRollAudioCaptureDevice {
        doc /* Push-to-talk IAudioCaptureDevice decorator that keeps its underlying device capturing between Stop and
             * the next Start, remembers the most recent pre-roll (300 ms by default, configurable, zero to disable)
             * of audio no subscriber received, and delivers it as the first FrameCaptured block of the next run so
             * speech that begins as a button is pressed is not clipped. Disposable, to close the microphone it keeps
             * open between runs. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/PreRollAudioCaptureDevice.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/PreRollAudioCaptureDeviceTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/pre-roll-audio-capture-device.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/pre-roll-audio-capture-device.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/pre-roll-audio-capture-device.yaml");
        }

        // Implements and wraps the capture contract, and reports an unavailable underlying device with the
        // library's unavailable-device exception.
        dependency PreRollAudioCaptureDevice to IAudioCaptureDevice;
        dependency PreRollAudioCaptureDevice to AudioDeviceUnavailableException;
    }
}
