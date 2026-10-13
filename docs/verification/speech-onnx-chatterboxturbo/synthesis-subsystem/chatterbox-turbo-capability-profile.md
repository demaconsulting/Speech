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
long sentences are split (never at a digit-flanked stop, and by word budget when a clause has no punctuation);
mood tags mark following text, apply until the next mood tag, add the join silence only at mood changes and never
leak marker characters; a trailing tag-only chunk joins the previous unit; null arguments throw;
empty or invalid input is safe.

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

##### A full stop between digits is not a sentence boundary

**Test**: `ChatterboxTurboCapabilityProfile_Render_DigitFlankedStop_IsNotASentenceBoundary`

A decimal such as `3.14` stays inside one segment.

##### An over-long clause is split on the word budget

**Test**: `ChatterboxTurboCapabilityProfile_Render_OverLongClause_SplitsOnWordBudgetKeepingEveryWord`

A clause with no clause punctuation and more than 200 characters is split by word budget and every word is
kept. The 200-character budget is measured on placeholder text, where each native tag counts as one
character, so the expanded model tokens are not counted and a part may exceed the budget by the tags' expansion.

##### Empty or invalid input is safe

**Test**: `ChatterboxTurboCapabilityProfile_Render_EmptyOrInvalid_BehavesSafely`

##### Treatments cover every tag without emotion tokens

**Test**: `ChatterboxTurboCapabilityProfile_Treatments_CoverEveryTagWithoutEmotionTokens`

##### Mood tags mark text and non-mood style tags stay stripped

**Tests**: `ChatterboxTurboCapabilityProfile_Render_MoodTag_MarksFollowingTextWithMood` and
`ChatterboxTurboCapabilityProfile_Render_NonMoodStyleTags_StayStripped`

Each of the 15 mood tags marks the following text with its mood (aliases map to 13 distinct moods); whispers,
sarcastic, soft and breathy stay stripped.

##### A mood applies until the next mood tag

**Tests**: `ChatterboxTurboCapabilityProfile_Render_MoodRuns_AppliesUntilNextMoodTagInOrder` and
`ChatterboxTurboCapabilityProfile_Render_ConsecutiveMoodTags_LastWins`

##### Mood changes add a join silence

**Tests**: `ChatterboxTurboCapabilityProfile_Render_MoodChange_AddsJoinSilenceOnlyAtChanges`,
`ChatterboxTurboCapabilityProfile_Render_JoinSilence_ZeroDisablesAndLongerEllipsisPauseWins` and
`ChatterboxTurboCapabilityProfile_Render_PauseBetweenMoods_IsUnmarkedAndNotDoubled`

Adjacent spoken segments with different moods get the join silence and none otherwise; zero disables it, a
longer ellipsis pause is kept and a pause between moods stays unmarked.

##### The end-of-sentence policy is applied per mood segment

**Test**: `ChatterboxTurboCapabilityProfile_Render_NativeTagInMoodSpan_KeepsPolicyPerSegment`

##### Marker characters in user text are removed

**Test**: `ChatterboxTurboCapabilityProfile_Render_MarkerCharactersInUserText_AreStripped`

##### The mood marker round-trips and the configuration is validated

**Tests**: `ChatterboxTurboCapabilityProfile_MoodMarker_RoundTripsAndJoinIsValidated`,
`ChatterboxTurboCapabilityProfile_WithMoodMarker_IsCaseInsensitiveAndTrimmed` and
`ChatterboxTurboCapabilityProfile_Constructor_JoinBoundariesAcceptedAndUndefinedPolicyRejected`

Mood names match case-insensitively after trimming (blank names add no marker) and the join boundaries 0 and
1000 are accepted.

##### A trailing tag-only chunk joins the previous unit

**Test**: `ChatterboxTurboCapabilityProfile_Render_TrailingTagOnlyChunk_JoinsPreviousUnit`

##### Render rejects null arguments

**Test**: `ChatterboxTurboCapabilityProfile_Render_NullArguments_Throw`
