package Speech_AudioSubsystem {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioClip::*;
    private import Speech_AudioDeviceDescription::*;
    private import Speech_AudioDeviceFactory::*;
    private import Speech_AudioDeviceInUseException::*;
    private import Speech_AudioDeviceSelection::*;
    private import Speech_AudioDeviceUnavailableException::*;
    private import Speech_AudioFileReader::*;
    private import Speech_AudioFormat::*;
    private import Speech_AudioResampler::*;
    private import Speech_FlacDecoder::*;
    private import Speech_IAudioCaptureDevice::*;
    private import Speech_IAudioCaptureDeviceProbe::*;
    private import Speech_IAudioPlaybackDevice::*;
    private import Speech_IAudioPlaybackDeviceProbe::*;
    private import Speech_PortAudio::*;
    private import Speech_PortAudioCaptureDevice::*;
    private import Speech_PortAudioCaptureDeviceProbe::*;
    private import Speech_PortAudioPlaybackDevice::*;
    private import Speech_PortAudioPlaybackDeviceProbe::*;
    private import Speech_UnavailableAudioCaptureDevice::*;
    private import Speech_UnavailableAudioCaptureDeviceProbe::*;
    private import Speech_UnavailableAudioPlaybackDevice::*;
    private import Speech_UnavailableAudioPlaybackDeviceProbe::*;
    private import Speech_WavDecoder::*;
    private import Speech_WavFileAudioCaptureDevice::*;
    private import Speech_WavFileAudioPlaybackDevice::*;
    private import Speech_WindowedSincLowpassFilter::*;

    part def AudioSubsystem {
        doc /* Audio capture/playback device and probe contracts, persisted name-based selection, honest unavailable
             * fallbacks, real PortAudio-backed implementations, and the internal PortAudio child subsystem that
             * isolates native interop behind a seam. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech/audio-subsystem.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem.yaml");
        }

        part audioFormat : AudioFormat;
        part windowedSincLowpassFilter : WindowedSincLowpassFilter;
        part audioDeviceDescription : AudioDeviceDescription;
        part audioDeviceSelection : AudioDeviceSelection;
        part iAudioCaptureDevice : IAudioCaptureDevice;
        part iAudioPlaybackDevice : IAudioPlaybackDevice;
        part iAudioCaptureDeviceProbe : IAudioCaptureDeviceProbe;
        part iAudioPlaybackDeviceProbe : IAudioPlaybackDeviceProbe;
        part audioDeviceFactory : AudioDeviceFactory;
        part portAudioCaptureDeviceProbe : PortAudioCaptureDeviceProbe;
        part portAudioPlaybackDeviceProbe : PortAudioPlaybackDeviceProbe;
        part portAudioCaptureDevice : PortAudioCaptureDevice;
        part portAudioPlaybackDevice : PortAudioPlaybackDevice;
        part unavailableAudioCaptureDevice : UnavailableAudioCaptureDevice;
        part unavailableAudioPlaybackDevice : UnavailableAudioPlaybackDevice;
        part unavailableAudioCaptureDeviceProbe : UnavailableAudioCaptureDeviceProbe;
        part unavailableAudioPlaybackDeviceProbe : UnavailableAudioPlaybackDeviceProbe;
        part audioDeviceUnavailableException : AudioDeviceUnavailableException;
        part audioDeviceInUseException : AudioDeviceInUseException;
        part wavFileAudioPlaybackDevice : WavFileAudioPlaybackDevice;
        part wavFileAudioCaptureDevice : WavFileAudioCaptureDevice;
        part audioFileReader : AudioFileReader;
        part audioClip : AudioClip;
        part audioResampler : AudioResampler;
        part wavDecoder : WavDecoder;
        part flacDecoder : FlacDecoder;
        part portAudio : PortAudio;
    }
}
