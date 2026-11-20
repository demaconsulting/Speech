<!-- markdownlint-disable table-too-wide -->

# Generalization set writing prompt

One sub-agent of a different model family than the developer writes the blind generalization set of the word-cluster
work (PR A). It sees only `readings.tsv`, `forms.tsv` and this prompt, staged in a folder outside the repository, and
no training, development, holdout, regression, model, evaluation or cluster data. The protocol (staging, the novelty
check of the staged copy, the blind label review and the freeze) is in the trainer's `RUN-LOG.md`, entry "Word-cluster
subsystem (PR A)". This prompt was committed before the set was written.

| Field | Value |
| --- | --- |
| Operator | GitHub Copilot CLI session orchestrated for DEMA Consulting |
| Agent type | `general-purpose` |
| Model | `gpt-5.6-luna` (planned) |
| Dispatched (UTC) | recorded here when the writer is dispatched |
| Completed (UTC) | recorded here when the set is frozen |
| Source | The prompt text below is given to the writer verbatim. Only the staging folder is replaced by `<STAGE>`. |

## Prompt text

```text
You are writing a BLIND GENERALIZATION TEST SET for an English heteronym pronunciation classifier: for one word in each
sentence, the classifier decides which pronunciation (reading) applies. The set measures whether the classifier handles
words and constructions it has never seen. Independence is the whole point: you must NOT look at any training data.

WORKSPACE. Work ONLY in <STAGE>. Allowed reads: <STAGE>\readings.tsv (reading_id, lemma, pos_hint, description, status),
<STAGE>\forms.tsv (form, reading_id, kind: the surface forms each reading can take) and <STAGE>\generalization.md (this
prompt). Write ONLY under <STAGE>\out\. FORBIDDEN: any other file or folder on disk, any git repository, web search, and
any model, evaluation or cluster output. Write every sentence yourself, originally.

TARGETS. Write rows for these 13 target words and 27 target slots (a slot is a target word with one reading). The
reading ids are exact ids of readings.tsv; the lemma column of a row is the lemma of its reading in readings.tsv.

  target  | allowed target forms     | slots (reading_id, lemma column)
  tear    | tear, tears              | tear.drop (tear), tear.rip (tear)
  wound   | wound                    | wound.injury (wound), wind.coil (wind; "wound" as the past tense of wind)
  wind    | wind, winds              | wind.air (wind), wind.coil (wind)
  minute  | minute                   | minute.time (minute), minute.tiny (minute)
  read    | read                     | read.past (read), read.present (read)
  bass    | bass                     | bass.fish (bass), bass.music (bass)
  bow     | bow, bows                | bow.bend (bow), bow.weapon (bow)
  lead    | lead, leads              | lead.guide (lead), lead.metal (lead)
  row     | row, rows, rowed, rowing | row.argument (row), row.line (row)
  sow     | sow, sows                | sow.pig (sow), sow.plant (sow)
  close   | close, closer            | close.adj-noun (close), close.verb (close)
  live    | live                     | live.adj (live), live.verb (live)
  content | content, contents        | content.noun (content), content.satisfied (content)

Use only the allowed target forms: each is listed in forms.tsv for both readings of its target, so the classifier has
to choose. Read each reading's description in readings.tsv and use the target exactly in that reading.

QUOTAS. Write 9 rows per slot: 243 rows in total (some may be dropped later, so do not write fewer). In each slot:
- 5 rows of kind novel-vocabulary and 4 rows of kind novel-construction;
- at least 3 rows with cue_unsolvable=yes (see below), and at least one of them of each kind.

KINDS.
- novel-vocabulary: the reading is signaled by one to three designated CONTENT words of the sentence (nouns, verbs,
  adjectives or adverbs, never function words), which you list in cue_forms. Prefer specific, less frequent words
  (domain terms of anatomy, sewing and textiles, sport, music, clocks and machines, angling, farming, chemistry, law,
  sailing and so on) over the most obvious everyday cue words, because the obvious ones are likely to be known to the
  classifier already and such a row would be rejected and sent back to you.
- novel-construction: the reading is signaled by the grammar or the arrangement of the words right around the target,
  not by a designated content word. Vary the two words before and the two words after the target: avoid the most
  common frames (such as "the X of", "to X the", "a X in", "will X the") and use less usual but natural ones (a
  fronted phrase, an inversion, a question, an apposition, a coordination of verbs or nouns, a passive, a relative
  clause, an imperative with an adverb). Write cue_forms as "-" for these rows.

FAMILIES. Every row has a family "{target}:{cue-type}", lower-case ASCII letters, digits and hyphens, where {target} is
the target word of the table above (for example "tear:body-part", "tear:crying", "minute:measurement",
"read:time-phrase", "wound:clockwork", "bass:angling"). A family groups the rows that share one kind of cue or one
template idea; it may hold rows of both readings of a target. Give each target 3 to 6 families, and each family 3 to 9
rows (never more than 12). In total there must be at least 30 families.

CUE-UNSOLVABLE ROWS (cue_unsolvable=yes). The classifier has a few hand-written cues. In a cue_unsolvable=yes row, a
careful human reader must still be able to tell the reading from the meaning, but NONE of the following may occur:
- anywhere in the sentence, any of these degree adverbs: very, so, too, quite, extremely, rather, most, more, less,
  really, incredibly, exceedingly, remarkably, fairly, somewhat, highly, utterly, terribly, awfully, perfectly,
  completely, entirely, absolutely, particularly, especially, unusually, surprisingly, equally, pretty;
- anywhere in the sentence, the past-time words yesterday, ago, previously, and the word last (in any use);
- anywhere in the sentence, any of these words: muscle, muscles, ligament, ligaments, tendon, tendons, hamstring,
  meniscus, cartilage, rotator, cuff, acl, labrum, groin, fabric, cloth, canvas, sail, seam, sleeve, lining, denim,
  wear;
- a coordinating conjunction (and, or, but, nor, yet) directly before or directly after the target word;
- the target word "wound" used as a noun (an injury): a cue_unsolvable=yes row of the wound target uses "wound" as a
  verb (the past tense of wind, or the verb "to wound": "careless words can wound").
Rows with cue_unsolvable=no may use any words, including the ones above.

EVERY ROW.
- One sentence of 6 to 30 words (count runs of letters and digits), plain ASCII only (straight quotes and apostrophes,
  no tab, no other control character).
- The target occurs exactly once, as a whole word, and no other form of the same target word occurs in the sentence (for
  the wound and wind targets, none of the other words wound, wounds, wounded, wind, winds, winding may occur besides the
  target; for live, do not use "lives").
- Original text: no quotation, no lyrics, no famous lines, no copyrighted text, no real living person; neutral content.
- Vary register (news, technical, conversational, literary, instructions, headlines, questions), length, the position
  of the target, and the readings' parts of speech where the description allows more than one.
- No two rows share the same sentence, and do not write near copies of your own rows.

OUTPUT. Write <STAGE>\out\generalization.tsv: UTF-8 without byte order mark, LF line endings, a final newline, tab
separated, no quoting, with exactly this header line:

id	lemma	reading_id	sentence	start	end	kind	family	cue_forms	cue_unsolvable	source	license

- id: gen-0001, gen-0002, ... in file order (four digits);
- lemma: the lemma column of readings.tsv for reading_id (for example "wind" for wind.coil, even when the target is
  "wound");
- start, end: the character offsets of the target in the sentence, end exclusive (Python sentence[start:end] is the
  target; with ASCII text these are also the UTF-16 offsets);
- kind: novel-vocabulary or novel-construction;
- family: as above;
- cue_forms: for novel-vocabulary, the designated cue words, lower-case, exactly as they appear in the sentence (each
  a single word of letters and digits, not a form of the target), separated by "|"; for novel-construction, "-";
- cue_unsolvable: yes or no;
- source: generalization;
- license: CC0-1.0.

Write in batches by target and append, so progress is not lost.

SELF-VALIDATION. Write and run a Python validator under <STAGE>\out\ and fix every problem it reports before you
finish. It must check: the exact header; sequential unique ids; that reading_id exists in readings.tsv with the given
lemma; that sentence[start:end].lower() is a form listed in forms.tsv for reading_id and that the characters just
before and after the span are not letters or digits; exactly one target word of the target's forms in the sentence;
6 to 30 words; ASCII only and no tab; the kind, family pattern, cue_forms rules (each cue form a lower-case word of the
sentence and not a form of the target; "-" only for novel-construction), cue_unsolvable, source and license values; for
cue_unsolvable=yes rows none of the forbidden words (whole words, case-insensitive), no and/or/but/nor/yet next to the
target, and no noun "wound"; 9 rows per slot with 5 novel-vocabulary, 4 novel-construction and at least 3
cue_unsolvable=yes rows (at least one of each kind); 3 to 9 rows per family and at least 30 families; no duplicate
sentence (compare lower-cased letters and digits only).

Also write <STAGE>\out\notes.md with your model name, the time you started and finished, and the validator's final
output. Do not explain your labels anywhere else.

Return ONLY: rows written, slots covered, families, rows per kind, cue_unsolvable rows, and the validator result.

LATER ROUNDS. After you finish, a checker compares your rows with data you cannot see. It may send back row ids with a
generic reason (for example "the sentence repeats or nearly repeats an existing sentence; write a new sentence", "cue
form 'x' already occurs with this lemma in the training or development data; choose a different cue word" or "the words
right around the target already occur around this word in the training or development data; change the construction").
Then replace exactly those rows in place (same id, same slot, same kind and family, same cue_unsolvable value), keep
every rule above, rerun the validator and return the same summary.
```
