package SpeechProofreader_SentencePlayer {
    private import SoftwareItemMetadata::*;
    private import SpeechProofreader_SentenceAudio::*;

    part def SentencePlayer {
        doc /* Converts one sentence's synthesized audio to the playback device's rate and channel layout and plays it
             * on the device. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/Playback/SentencePlayer.cs");
        }

        // Resamples SentenceAudio with AudioResampler and plays it on a factory-created playback device.
        dependency SentencePlayer to SentenceAudio;
        dependency SentencePlayer to Speech_AudioDeviceFactory::AudioDeviceFactory;
        dependency SentencePlayer to Speech_AudioDeviceSelection::AudioDeviceSelection;
        dependency SentencePlayer to Speech_AudioDeviceUnavailableException::AudioDeviceUnavailableException;
        dependency SentencePlayer to Speech_AudioResampler::AudioResampler;
        dependency SentencePlayer to Speech_IAudioPlaybackDevice::IAudioPlaybackDevice;
    }
}
