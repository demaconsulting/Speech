# Speech-to-Text Evaluation Data Attribution

The audio files of `data/stt-en/` come from two sources. Each row of `MANIFEST.tsv` names its source and license.

## LibriSpeech (set `librispeech-test-clean`)

License: CC-BY-4.0

The files of `librispeech-test-clean/` and their reference transcripts are taken from the following work, which is
licensed under the Creative Commons Attribution 4.0 International License.

- **Title:** LibriSpeech ASR corpus, `test-clean` set
- **Creators:** Vassil Panayotov, Guoguo Chen, Daniel Povey and Sanjeev Khudanpur; the audio is read by LibriVox
  volunteers from public-domain books of Project Gutenberg
- **Source:** <https://www.openslr.org/12>; the archive used is <https://www.openslr.org/resources/12/test-clean.tar.gz>
- **License:** Creative Commons Attribution 4.0 International (CC BY 4.0),
  <https://creativecommons.org/licenses/by/4.0/> ("LibriSpeech (c) 2014 by Vassil Panayotov", `LICENSE.TXT` of the
  archive)
- **Citation:** Vassil Panayotov, Guoguo Chen, Daniel Povey and Sanjeev Khudanpur, "Librispeech: an ASR corpus based on
  public domain audio books", Proceedings of the IEEE International Conference on Acoustics, Speech and Signal
  Processing (ICASSP 2015), pages 5206-5210

### Changes Made

A subset of 24 utterances was selected by the rule in `README.md`. The audio files are unchanged (byte-identical to the
archive) and the reference transcripts are the corpus transcript lines, unchanged. The authors of LibriSpeech do not
endorse this project.

## Crossing the Bar Recitation (set `crossing-the-bar`)

License: CC0-1.0

- **Source:** [voxserv/audio_quality_testing_samples](https://github.com/voxserv/audio_quality_testing_samples)
  (`orig/382326__scott-simpson__crossing-the-bar.wav`), originally hosted on
  [freesound.org](https://freesound.org/people/Scott%20Simpson/sounds/382326/)
- **Author:** Scott Simpson (freesound.org user "Scott Simpson")
- **License:** CC0 1.0 Universal (Public Domain Dedication)
- **Content:** a studio-quality spoken-word recitation of "Crossing the Bar" by Alfred, Lord Tennyson (published 1889;
  the poem text is in the public domain)
- **Modifications made in this repository:** downmixed from stereo to mono and resampled from 44100 Hz to 16000 Hz
  (16-bit PCM) to match the sample rate expected by this library's recognition models; no other content changes
- **Purpose:** a genuine human-speech accuracy fixture for the recognition model tests and the evaluation tool
