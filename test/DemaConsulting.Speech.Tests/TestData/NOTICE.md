# Third-Party Test Assets

The `crossing-the-bar-16k-mono.wav` recording these tests use is linked from `data/stt-en/`; its source and license
(CC0 1.0) are in `data/stt-en/ATTRIBUTION.md`.

## flac/*.flac and flac/*.wav, and the ChatterboxTurbo test `voice-mono-16-16000.flac`

- **Source**: generated for this repository; no third-party audio is included.
- **License**: original synthetic content, released under this repository's MIT license.
- **Content**: deterministic synthetic signals (440 Hz and 1330 Hz sine tones plus low-level
  Gaussian noise from a fixed random seed, 660 Hz in the right channel of the plain stereo files,
  and a 0.9-scaled copy of the left channel for `stereo-correlated-16`). No speech is included.
- **Recipe**: the Python `soundfile` package 0.14.0 (libsndfile 1.2.2, which wraps libFLAC) wrote each
  signal as a FLAC file and, from the very same samples, as a lossless PCM WAV file (8-bit unsigned,
  16-bit or 24-bit). The generator asserted that libsndfile decodes both files to identical samples.
  `stereo-16-compression8` was written with `compression_level=1.0` (libFLAC level 8, which exercises
  high-order LPC predictors); the others use the libsndfile default level. Formats covered:
  `mono-16-44100`, `stereo-16-48000`, `mono-24-48000`, `mono-8-8000`, `stereo-16-compression8`,
  `stereo-correlated-16` (each as `.flac` and `.wav`), plus the 1.2 s, 16 kHz mono
  `voice-mono-16-16000.flac` used as a reference-voice fixture by the ChatterboxTurbo tests.
- **Purpose**: independent, encoder-produced ground truth proving the managed FLAC decoder is
  bit-exact against libFLAC's own output, and exercising WAV/FLAC reference-voice loading.
