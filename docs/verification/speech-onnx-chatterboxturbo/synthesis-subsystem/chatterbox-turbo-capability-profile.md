### ChatterboxTurboCapabilityProfile Verification

#### Verification Approach

Verified by `ChatterboxTurboCapabilityProfileTests.cs`, rendering sample texts and asserting the
produced segments. Nothing is mocked: the profile is a pure function. Where a test needs an
`ISpeechModel` for rendering, it passes the real `OnnxChatterboxTurboEnglishSynthesisModel` (as an
`ISpeechModel`), so the capability profile is exercised exactly as the Speech renderer uses it.

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

**Tests**: `ChatterboxTurboCapabilityProfile_Render_LaughTag_UsesModelToken`,
`ChatterboxTurboCapabilityProfile_Render_ClearThroatTag_UsesModelToken` and
`ChatterboxTurboCapabilityProfile_Render_GaspTag_UsesModelToken`

##### Unsupported tags never emit bracket text

**Test**: `ChatterboxTurboCapabilityProfile_Render_UnsupportedTags_NeverEmitBracketText`

##### A sigh becomes a silence segment

**Test**: `ChatterboxTurboCapabilityProfile_Render_Sigh_BecomesSilenceSegment`

##### Pause tags become silence

**Tests**: `ChatterboxTurboCapabilityProfile_Render_ShortPause_BecomesThreeHundredMillisecondSilence` and
`ChatterboxTurboCapabilityProfile_Render_LongPause_BecomesNineHundredMillisecondSilence`

##### Trailing tag, move-before-final-word policy

**Test**: `ChatterboxTurboCapabilityProfile_Render_TrailingTag_MoveBeforeFinalWord_RelocatesTag`

##### Trailing tag, suffix policy

**Tests**: `ChatterboxTurboCapabilityProfile_Render_TrailingTag_Suffix_AppendsSuffixedTag` (laugh gains
its suffixed form) and
`ChatterboxTurboCapabilityProfile_Render_TrailingTag_SuffixWithoutSuffixForm_DropsTag` (gasp and clear
throat have no suffixed form and are dropped)

##### Trailing tag, strip policy

**Test**: `ChatterboxTurboCapabilityProfile_Render_TrailingTag_Strip_DropsTag`

##### A tag between sentences leads the next sentence

**Test**: `ChatterboxTurboCapabilityProfile_Render_TagBetweenSentences_LeadsNextSentence`

##### Sentences are split

**Test**: `ChatterboxTurboCapabilityProfile_Render_Sentences_AreSplit`

##### An ellipsis sentence gets a pause

**Test**: `ChatterboxTurboCapabilityProfile_Render_EllipsisSentence_GetsFiveHundredMillisecondPause`

##### A long sentence is split keeping every word

**Test**: `ChatterboxTurboCapabilityProfile_Render_LongSentence_IsSplitKeepingEveryWord`

##### A full stop between digits is not a sentence boundary

**Test**: `ChatterboxTurboCapabilityProfile_Render_DigitFlankedStop_IsNotASentenceBoundary`

A decimal such as `3.14` stays inside one segment.

##### An over-long clause is split on the word budget

**Test**: `ChatterboxTurboCapabilityProfile_Render_OverLongClause_SplitsOnWordBudgetKeepingEveryWord`,
`ChatterboxTurboCapabilityProfile_Render_SingleOverLongToken_IsCutToTheLimit` (a 450-character run becomes three
units) and `ChatterboxTurboCapabilityProfile_Render_TagExpansion_MayExceedBudgetByTheExpansionOnly`

A clause with no clause punctuation and more than 200 characters is split by word budget and every word is
kept. The 200-character budget is measured on placeholder text, where each native tag counts as one
character, so the expanded model tokens are not counted and a part may exceed the budget by the tags' expansion.

##### Clause punctuation splits long sentences

**Tests**: `ChatterboxTurboCapabilityProfile_Render_LongSentenceWithClauses_SplitsAtClauseBoundaries`
(a long sentence with `,`, `;` and `:` clauses splits at those boundaries into exactly two segments) and
`ChatterboxTurboCapabilityProfile_Render_DigitFlankedClauseBoundary_StaysInsideItsUnit` (a comma or colon between
digits, as in `1,000` and `12:30`, is not a clause boundary)

##### Empty or invalid input is safe

**Tests**: `ChatterboxTurboCapabilityProfile_Render_EmptyInput_ProducesEmptyPlan` and
`ChatterboxTurboCapabilityProfile_Render_WhitespaceOnlyText_ProducesEmptyPlan`

##### A tag with no neighboring speech is dropped

**Test**: `ChatterboxTurboCapabilityProfile_Render_TagWithNoSpeech_IsDropped`

##### Treatments cover every tag without emotion tokens

**Test**: `ChatterboxTurboCapabilityProfile_Treatments_CoverEveryTagWithoutEmotionTokens`

##### Mood tags mark text and non-mood style tags stay stripped

**Tests**: `ChatterboxTurboCapabilityProfile_Render_MoodTag_MarksFollowingTextWithMood` and
`ChatterboxTurboCapabilityProfile_Render_NonMoodStyleTags_StayStripped`

Each of the 15 mood tags marks the following text with its mood; whispers, sarcastic, soft and breathy stay
stripped.

##### Fifteen mood tags map to thirteen distinct moods

**Tests**: `ChatterboxTurboCapabilityProfile_Treatments_FifteenMoodTagsMapToThirteenDistinctMoods` (asserts
exactly 13 distinct mood names) and
`ChatterboxTurboCapabilityProfile_Treatments_AliasTags_ShareTheirTargetMood` (panicked shares worried's mood,
serious shares neutral's)

##### A mood applies until the next mood tag

**Tests**: `ChatterboxTurboCapabilityProfile_Render_MoodRuns_AppliesUntilNextMoodTagInOrder` and
`ChatterboxTurboCapabilityProfile_Render_ConsecutiveMoodTags_LastWins`

##### Mood changes add a join silence

**Tests**: `ChatterboxTurboCapabilityProfile_Render_MoodChange_AddsJoinSilenceOnlyAtChanges`,
`ChatterboxTurboCapabilityProfile_Render_JoinSilenceZero_AddsNoSilenceAtMoodChange`,
`ChatterboxTurboCapabilityProfile_Render_EllipsisPauseLongerThanJoin_WinsAtMoodChange` and
`ChatterboxTurboCapabilityProfile_Render_PauseBetweenMoods_IsUnmarkedAndNotDoubled`

Adjacent spoken segments with different moods get the join silence and none otherwise; zero disables it, a
longer ellipsis pause is kept and a pause between moods stays unmarked and is not doubled.

##### The end-of-sentence policy is applied per mood segment

**Test**: `ChatterboxTurboCapabilityProfile_Render_NativeTagInMoodSpan_KeepsPolicyPerSegment`

##### Marker and placeholder characters in user text are removed

**Tests**: `ChatterboxTurboCapabilityProfile_Render_MarkerCharactersInUserText_AreStripped` and
`ChatterboxTurboCapabilityProfile_Render_PlaceholderCharactersInUserText_AreRemoved`

##### The mood marker round-trips and unknown moods add no marker

**Tests**: `ChatterboxTurboCapabilityProfile_MoodMarker_RoundTripsEveryKnownMood`,
`ChatterboxTurboCapabilityProfile_ReadMoodMarker_TextWithoutMarker_ReturnsTextAndNoMood`,
`ChatterboxTurboCapabilityProfile_WithMoodMarker_IsCaseInsensitiveAndTrimmed` and
`ChatterboxTurboCapabilityProfile_WithMoodMarker_NullBlankOrUnknownMood_AddsNoMarker`

Mood names match case-insensitively after trimming; null, blank and unknown names add no marker.

##### The configuration is validated

**Tests**: `ChatterboxTurboCapabilityProfile_Constructor_JoinBoundaries_AreAccepted` (0 and 1000),
`ChatterboxTurboCapabilityProfile_Constructor_JoinOutsideRange_ThrowsArgumentOutOfRangeException` (-1 and
1001) and `ChatterboxTurboCapabilityProfile_Constructor_UndefinedPolicy_ThrowsArgumentOutOfRangeException`

##### A trailing tag-only chunk joins the previous unit

**Test**: `ChatterboxTurboCapabilityProfile_Render_TrailingTagOnlyChunk_JoinsPreviousUnit`

##### Render rejects null arguments

**Test**: `ChatterboxTurboCapabilityProfile_Render_NullArguments_Throw`
