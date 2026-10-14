#### PortAudioApi

##### Verification Approach

Verified directly through unit tests of the adapter's deterministic logic (metadata conversion, sample-rate
conversion, count validation, callback marshalling and format probing), and indirectly through higher-level tests
that consume the `IPortAudioApi` seam. Opening a real native stream needs the native library and audio hardware and
is covered by manual hardware verification only.

##### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- Native memory is allocated by the tests; no audio hardware or native PortAudio library is required.

##### Acceptance Criteria

The adapter is considered verified when each conversion produces the expected managed value, handler faults abort
the stream instead of escaping, and the shared singleton loads.

##### Test Scenarios

##### Adapter: Native Metadata and Value Conversion

**Tests**: `PortAudioApi_ConvertHostApiInfo_NativeBlock_ReturnsManagedMetadata`,
`PortAudioApi_ConvertHostApiInfo_NullName_ReturnsEmptyName`,
`PortAudioApi_ConvertSampleRate_FractionalRate_RoundsToWholeHertz`,
`PortAudioApi_NormalizeCount_NegativeCount_ThrowsWithContext`

##### Adapter: Callback Marshalling and Fault Containment

**Tests**: `PortAudioApi_CaptureCallback_NativeInput_DeliversManagedSamples`,
`PortAudioApi_CaptureCallback_NullInputAfterData_DeliversSilence`,
`PortAudioApi_CaptureCallback_HandlerThrows_ReturnsAbort`,
`PortAudioApi_PlaybackCallback_ProvidedSamples_AreWrittenToNativeOutput`,
`PortAudioApi_PlaybackCallback_ProviderThrows_ReturnsAbort`

##### Adapter: Format Probing

**Tests**: `PortAudioApi_IsFormatSupported_NativeResult_MapsToSupport`,
`PortAudioApi_IsFormatSupported_NativeCallThrows_ReturnsFalse`

##### Singleton: Read Twice Returns Same Instance

**Test**: `PortAudioApi_Instance_ReadTwice_ReturnsSameInstance`

##### Integration: Higher-Level Seam Logic Uses the Contract Successfully

**Tests**: `AudioDeviceFactory_Constructor_PortAudioInitialized_ExposesRealProbes`,
`AudioDeviceFactory_CreateCaptureDevice_PortAudioInitialized_ReturnsRealDevice`,
`AudioDeviceFactory_CreatePlaybackDevice_PortAudioInitialized_ReturnsRealDevice`

##### Integration: Terminate Is Exercised Through a Refresh

**Tests**: `PortAudioEnvironment_Refresh_NoActiveStreams_ReinitializesAndReflectsNewOutcome`,
`PortAudioEnvironment_Refresh_NotPreviouslyInitialized_SkipsTerminateAndReinitializes`
