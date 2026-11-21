# Test Data Provenance

## nemotron-features-golden.tsv

- **Source**: generated for this repository; no third-party audio, values or code are included.
- **License**: original content derived from our own synthetic signals, released under this repository's MIT license.
- **Content**: reference log-mel feature values (one row per frame: signal name, frame index, 128 natural-log band
  values, 5 decimals) for four deterministic synthetic signals: `two-tone` (1600 samples, `0.3 sin(440 Hz) + 0.1
  sin(1234 Hz)`), `chirp` (3200 samples, amplitude 0.5, linear sweep from 100 Hz to 7000 Hz), `silence` (800 zero
  samples) and `short` (100 samples, amplitude 0.5, 1000 Hz), all at 16 kHz and rounded to 32-bit floats. No speech is
  included.
- **Recipe**: an independent implementation of the documented NeMo `FilterbankFeatures` inference formula written for
  this repository (NeMo itself is not installed or run), using PyTorch 2.13 and librosa 1.0 in 64-bit floating point:
  pre-emphasis `y[0] = x[0]`, `y[n] = x[n] - 0.97 x[n-1]`; the PyTorch short-time Fourier transform of `y` with a
  512-point FFT, a 160-sample hop, a 400-sample symmetric Hann window (`torch.hann_window(400, periodic=False)`,
  centered in the 512 points), `center=True` and `pad_mode="constant"`; power `|S|^2`; the librosa mel filterbank
  (16 kHz, 512-point FFT, 128 bands from 0 Hz to 8000 Hz, Slaney mel scale, `norm="slaney"`) applied to the power
  spectrum; `log(mel + 2^-24)`. The signals are built in 64-bit floats with `numpy.sin` over
  `n = 0..N-1` (the chirp phase is `2 pi (100 t + 6900 t^2 / (2 * 0.2 s))`, `t = n / 16000`) and then cast to 32-bit.
- **Purpose**: proves the managed feature extractor reproduces the NeMo front-end semantics the Nemotron model was
  trained with (constant center padding, symmetric Hann window, 2^-24 log guard) to within 1e-3 on every value.
