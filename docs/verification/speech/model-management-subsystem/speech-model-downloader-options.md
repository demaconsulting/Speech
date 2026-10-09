### SpeechModelDownloaderOptions

#### Verification Approach

Verified indirectly through `SpeechModelDownloader`'s own URI-resolution scenarios: a `null`
`Mirror` (the default) must forward the original, model-declared URI unchanged, while a configured
`Mirror` must resolve and forward the mirror-relative effective URI. Mirror authentication itself
is applied by `HttpModelDownloadClient` at construction time (see `http-model-download-client.md`),
not forwarded per call, so these scenarios only assert the resolved request URI. A caller-supplied
`IModelDownloadClient` combined with a credentialed `Mirror` (`Credentials` or `BearerToken` set)
is rejected at construction, since that combination would otherwise silently configure a
credential that is never actually applied to any outgoing request.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK

#### Acceptance Criteria

Tests pass when a `SpeechModelDownloader` constructed with no options (or with a `null` `Mirror`)
forwards each file's original, model-declared URI unchanged - byte-identical to the pre-mirror
implementation - a `SpeechModelDownloader` constructed with a configured, auth-free `Mirror`
forwards each file's mirror-resolved effective URI instead, and constructing one with both a
caller-supplied client and a credentialed `Mirror` throws `ArgumentException`.

#### Test Scenarios

##### DownloadAsync: No Mirror Configured Forwards Original URI Unchanged

**Test**: `SpeechModelDownloader_DownloadAsync_NoMirrorConfigured_ForwardsOriginalUriUnchanged`

##### DownloadAsync: Mirror Configured Forwards Effective URI

**Test**: `SpeechModelDownloader_DownloadAsync_MirrorConfigured_ForwardsEffectiveUri`

##### Constructor: Credentialed Mirror With Caller-Supplied Client Throws ArgumentException

**Test**: `SpeechModelDownloader_Constructor_CredentialedMirrorWithCallerSuppliedClient_ThrowsArgumentException`
