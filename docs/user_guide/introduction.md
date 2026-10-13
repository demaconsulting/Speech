# Introduction

## Purpose

This document is the user guide for DemaConsulting.Speech, a cross-platform .NET library
providing local, offline speech-to-text (STT) and text-to-speech (TTS) services for desktop
applications.

## Scope

This user guide covers:

- Installation of the library
- Managing per-user speech model storage and downloads, and the audio-device abstractions
- Recognizing speech from an audio input device
- Recognizing inline Natural Language Audio Tags in text intended for text-to-speech
- Synthesizing speech, including the Chatterbox Turbo reference voices
- Running the bundled demo application and the `speech-cli` command-line tool
- Runtime-support limitations and fallback behavior
- Where to find full API and design detail

This user guide does not cover:

- Cloud or network speech services (all recognition and synthesis is local and offline, apart from
  model downloads)
- Hardware-level audio verification, which is a manual activity (see "Hardware Verification Boundary")
- Distribution of the demo application, which is built from source and not published as a package
- Platforms or .NET versions other than the supported .NET 8, 9, and 10 desktop targets

# References

- [REF-1] Continuous Compliance Methodology (<https://github.com/demaconsulting/ContinuousCompliance>)
