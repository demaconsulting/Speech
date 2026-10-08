# Third-Party Test Assets

## crossing-the-bar-16k-mono.wav

- **Source**: [voxserv/audio_quality_testing_samples](https://github.com/voxserv/audio_quality_testing_samples)
  (`orig/382326__scott-simpson__crossing-the-bar.wav`), originally hosted on
  [freesound.org](https://freesound.org/people/Scott%20Simpson/sounds/382326/).
- **Author**: Scott Simpson (freesound.org user "Scott Simpson").
- **License**: CC0 1.0 Universal (Public Domain Dedication).
- **Content**: A studio-quality spoken-word recitation of "Crossing the Bar" by
  Alfred, Lord Tennyson (published 1889; poem text is in the public domain).
- **Modifications made in this repository**: downmixed from stereo to mono and
  resampled from 44100 Hz to 16000 Hz (16-bit PCM) to match the sample rate
  expected by this library's recognition models. No other content changes were
  made.
- **Purpose**: used as a genuine human-speech accuracy fixture for recognition
  model tests, verifying that the real, installed speech-to-text models
  transcribe real speech correctly (not just synthetic tones/silence).
