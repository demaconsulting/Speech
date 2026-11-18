### PiperVoiceConfig Verification

#### Verification Approach

`PiperVoiceConfig` is verified by unit tests in
`test/DemaConsulting.Speech.Onnx.Piper.Tests/ModelManagementSubsystem/PiperVoiceConfigTests.cs` against a trimmed copy
of the real configuration of the voice (`TestData/en_US-libritts_r-medium.onnx.trimmed.json`, CC BY 4.0, attributed
inside the file) and against small hand-written configurations for the defaults and the failure modes. The failure modes
are checked by the exception type and, for a missing framing symbol, by the symbol named in the message.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Files**: the trimmed configuration, copied next to the test assembly
- **Isolation**: no network, no model, no shared state

#### Acceptance Criteria

The real configuration parses to the sample rate 22,050, 904 speakers, the scales 0.333, 1.0 and 0.333, the espeak voice
`en-us`, 159 symbols and the begin, pad and end ids 1, 0 and 2; the stress mark is found, `x` and `X` have different ids
and a Chinese character is absent; a minimal configuration gets the speaker count 1, the scales 0.667, 1.0 and 0.8 and
an empty espeak voice; a configuration without `^`, `_` or `$` throws `InvalidDataException` naming the symbol; non-JSON
text, an array, a missing map, a missing or zero sample rate, the phoneme type `text` and a non-numeric id throw
`InvalidDataException`; a null text and an empty path throw argument errors; the file is read from its path.

#### Test Scenarios

##### Settings and symbol table

**Tests**: `PiperVoiceConfig_Parse_RealVoiceConfiguration_ReadsSettingsAndSymbolTable`,
`PiperVoiceConfig_TryGetIds_Symbols_AreOrdinalAndCaseSensitive`,
`PiperVoiceConfig_Parse_MinimalConfiguration_UsesDefaults`, `PiperVoiceConfig_Load_ConfigurationFile_ReadsIt`

##### Rejection

**Tests**: `PiperVoiceConfig_Parse_MissingFramingSymbol_ThrowsNamingTheSymbol`,
`PiperVoiceConfig_Parse_UnusableConfiguration_ThrowsInvalidData`, `PiperVoiceConfig_ParseAndLoad_InvalidArguments_Throw`
