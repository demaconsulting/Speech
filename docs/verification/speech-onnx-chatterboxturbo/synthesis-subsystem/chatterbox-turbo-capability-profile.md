### ChatterboxTurboCapabilityProfile Verification

#### Verification Approach

Verified by `ChatterboxTurboCapabilityProfileTests.cs`, rendering sample texts and asserting the
produced segments.

#### Test Environment

- **Framework**: xUnit v3; project `test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests`
- **Isolation**: pure functions; no files or network

#### Acceptance Criteria

Native tags use model tokens and never emit unsupported bracket text; sighs and pauses become
silence; trailing tags follow the configured policy; sentences are split with ellipsis pauses and
long sentences are split; empty or invalid input is safe.

#### Test Scenarios

##### A mid-sentence laugh stays inline

**Test**: `ChatterboxTurboCapabilityProfile_Render_MidSentenceLaugh_StaysInlineWithNativeToken`

##### Native tags use model tokens

**Test**: `ChatterboxTurboCapabilityProfile_Render_NativeTags_UseModelTokens`

##### Unsupported tags never emit bracket text

**Test**: `ChatterboxTurboCapabilityProfile_Render_UnsupportedTags_NeverEmitBracketText`

##### A sigh becomes a silence segment

**Test**: `ChatterboxTurboCapabilityProfile_Render_Sigh_BecomesSilenceSegment`

##### Pause tags become silence

**Test**: `ChatterboxTurboCapabilityProfile_Render_PauseTags_BecomeSilence`

##### Trailing tag, move-before-final-word policy

**Test**: `ChatterboxTurboCapabilityProfile_Render_TrailingTag_MoveBeforeFinalWord_RelocatesTag`

##### Trailing tag, suffix policy

**Test**: `ChatterboxTurboCapabilityProfile_Render_TrailingTag_Suffix_AppendsOrDrops`

##### Trailing tag, strip policy

**Test**: `ChatterboxTurboCapabilityProfile_Render_TrailingTag_Strip_DropsTag`

##### A tag between sentences leads the next sentence

**Test**: `ChatterboxTurboCapabilityProfile_Render_TagBetweenSentences_LeadsNextSentence`

##### Sentences split with an ellipsis pause

**Test**: `ChatterboxTurboCapabilityProfile_Render_Sentences_AreSplitWithEllipsisPause`

##### A long sentence is split

**Test**: `ChatterboxTurboCapabilityProfile_Render_LongSentence_IsSplit`

##### Empty or invalid input is safe

**Test**: `ChatterboxTurboCapabilityProfile_Render_EmptyOrInvalid_BehavesSafely`

##### Treatments cover every tag without emotion tokens

**Test**: `ChatterboxTurboCapabilityProfile_Treatments_CoverEveryTagWithoutEmotionTokens`
