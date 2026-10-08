### SpeechModelDownloaderOptions

#### Verification Approach

Verified indirectly through `SpeechModelDownloader`'s own mirror-forwarding scenarios: a `null`
`Mirror` (the default) must forward the original URI unchanged, while a configured `Mirror` must
redirect to the resolved effective URI and forward its authentication.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK

#### Acceptance Criteria

Tests pass when a `SpeechModelDownloader` constructed with no options (or with a `null` `Mirror`)
behaves byte-identically to the pre-mirror implementation, and a `SpeechModelDownloader`
constructed with a configured `Mirror` resolves every file beneath it and forwards the mirror's
authentication to the download client.

#### Test Scenarios

##### DownloadAsync: No Mirror Configured Forwards Original URI And Null Mirror Auth

**Test**: `SpeechModelDownloader_DownloadAsync_NoMirrorConfigured_ForwardsOriginalUriAndNullMirrorAuth`

##### DownloadAsync: Mirror Configured Forwards Effective URI And Mirror Auth

**Test**: `SpeechModelDownloader_DownloadAsync_MirrorConfigured_ForwardsEffectiveUriAndMirrorAuth`
