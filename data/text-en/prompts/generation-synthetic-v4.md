<!-- markdownlint-disable table-too-wide -->

# Synthetic sentence generation prompt, fourth round

Two sub-agents (`synth4-0`, `synth4-1`) receive this prompt, one part each, to write the targeted training rows of the
word-class work (PR B): 465 rows, balanced over the two readings of each target and written as contrastive pairs, for
the readings that the classifier confuses in context (part 0: tear, wound, minute and read, 285 rows; part 1:
bass, lead, close, live, wind and content, 180 rows). The two prompts differ only in the part number, the
lemmas and the file and id names. This prompt was committed before any row was written, together with the procedure in
the trainer's `RUN-LOG.md` (entry "PR B: procedure fixed before any training with word classes"); the record below is
completed after the round. The rows are reviewed blind by another model family
([review-synthetic-v4.md](review-synthetic-v4.md)).

| Field | Value |
| --- | --- |
| Operator | GitHub Copilot CLI session orchestrated for DEMA Consulting |
| Agent type | `general-purpose` |
| Model | `claude-sonnet-5.5` (writers, and new instances of the same model for any top-up round); blind review `gpt-6.1-sol` |
| Dispatched (UTC) | to be recorded |
| Completed (UTC) | to be recorded |
| Source | The prompt text below is given to each writer verbatim. Only the staging folder is replaced by `<STAGE>`. |

## Staging

The staging folder is outside the repository and holds only copies of `readings.tsv`, `forms.tsv`, `train.tsv` and
`dev.tsv` (the last two only to avoid duplicate sentences and templates), the quota file below as `quotas-v4.tsv`, the
two prompts below as `part-0-prompt.md` and `part-1-prompt.md`, and the review prompt as `review-prompt.md`. No holdout,
evaluation overlay, audit, prompt record, document, model or evaluation output is staged. A top-up writer receives the
same prompt with the generic first line described in its last paragraph, never a training, regression or generalization
sentence.

## Writing record

To be recorded after the round: the dispatch and completion times, the rows written, the rows removed by each mechanical
filter, the top-up rounds, the blind review and the rows kept, as in `RUN-LOG.md` (entry "PR B: synthetic-v4 data
round").

## Quota file

`quotas-v4.tsv` (tab-separated; `rows` per line, 285 rows in part 0 and 180 in part 1; every reading
of a lemma has the same number of rows):

```text
part	reading_id	lemma	target_forms	rows	focus
0	tear.rip	tear	tear|tears	12	noun: a tear in a part of the body (a muscle, ligament, tendon, cartilage, skin, retina, membrane and so on); vary the body-part words widely, use each at most twice, and use frames that also fit a teardrop (a tear in, on, along or across something; a small, deep or painful tear)
0	tear.rip	tear	tear|tears	12	noun: a tear in fabric, paper, canvas, a sail, a tent, a bag, a screen, a map, a page or another material
0	tear.rip	tear	tear|tears	5	noun: the fixed phrase wear and tear and similar uses
0	tear.rip	tear	tear|tears	6	noun: other rips, in sport reports, machines, seals, documents and plans
0	tear.rip	tear	tear|tears	10	verb: to rip, pull apart or rush (tear, tears), with varied objects and particles
0	tear.drop	tear	tear|tears	12	noun: a tear on a cheek, face or chin or in an eye, in frames that also fit a rip (a tear on, in, down or across something; a single or small tear)
0	tear.drop	tear	tear|tears	10	noun: tears of joy, grief, relief or laughter; holding back, wiping away or shedding tears
0	tear.drop	tear	tear|tears	8	noun as a modifier: tear gas, tear duct, tear film, tear stain and similar compounds
0	tear.drop	tear	tear|tears	15	noun: varied scenes of crying and emotion in fiction, news, medicine and poetry; at least 8 of these rows without the words cry, cried, crying, eye or eyes
0	wound.injury	wound	wound	23	noun after a determiner or modifier: a knife, stab, bullet, puncture, deep, open, fresh, gaping, entry or exit wound and so on; where possible the same determiners and modifiers as the wind.coil rows
0	wound.injury	wound	wound	10	verb: to injure, in the base form wound (can wound, will wound, to wound, do not wound)
0	wind.coil	wind	wound	10	past tense of wind with an object: wound the clock, the watch, the music box, the spring, the cable, the film
0	wind.coil	wind	wound	8	phrasal: wound up (ended up, or tense and excited) and wound down
0	wind.coil	wind	wound	7	past participle as a modifier or after a determiner: a tightly wound spring, the wound coil, a loosely wound ball, the freshly wound clock
0	wind.coil	wind	wound	7	wound something around or onto something (a bandage, rope, thread, wire), and paths or rivers that wound through, along or up something
0	minute.tiny	minute	minute	10	after a degree adverb: very, extremely, so, quite, rather, most, incredibly and others
0	minute.tiny	minute	minute	8	coordinated with another adjective (small and minute, minute but visible, minute or large)
0	minute.tiny	minute	minute	12	attributive before a noun (minute details, a minute crack, minute particles, minute amounts), including frames that also fit the time reading (a minute ..., the minute ...)
0	minute.time	minute	minute	8	the very minute, this minute, the last minute, at the minute, any minute
0	minute.time	minute	minute	8	every minute, a minute or two, per minute, one more minute, a minute later
0	minute.time	minute	minute	8	noun as a modifier: minute hand, minute mark, minute steak, minute rice, minute book
0	minute.time	minute	minute	6	verb: to minute a meeting, a decision or a remark (record it in the minutes)
0	read.past	read	read	10	a past-time expression before the target (yesterday, last night, two days ago, in 1998, when she was young, once)
0	read.past	read	read	8	a past-time expression after the target or at the end of the sentence
0	read.past	read	read	7	perfect: has, have or had read
0	read.past	read	read	4	passive: was, were, has been or will be read
0	read.past	read	read	6	no time word: the tense is shown by another past-tense verb of the same clause (she read the note and smiled; he read it twice before he signed)
0	read.present	read	read	7	habitual present (every night, usually, on Sundays, often)
0	read.present	read	read	7	after a modal or will, would, can, could, should or must
0	read.present	read	read	6	after to: wanted to read, likes to read, learned to read
0	read.present	read	read	6	after do, does or did: did not read, do you read, does she read
0	read.present	read	read	4	imperatives and the noun (a good read, a quick read)
0	read.present	read	read	5	a past-time expression in the sentence while the target is the base form: after an auxiliary, a modal or to (yesterday she could not read; two weeks ago we did not read), or in another clause joined by and, but, because or a semicolon (we read poetry, but last week it snowed)
1	bass.fish	bass	bass	15	the fish: cooking and food (fillet, grill, season, a plate of), angling and the animal's behavior (struck the lure, a hungry bass), with adjectives that also fit a sound (large, heavy, rich, striped, deep) in some rows
1	bass.music	bass	bass	15	low sound, voice, instrument or part: sound qualities (rich, heavy, deep, thumping), the player or singer in an ensemble (the principal bass, a new bass), short mentions in lists and coordinations (guitar and bass)
1	lead.guide	lead	lead|leads	15	rhymes with need: the noun (the lead car, a lead role, a narrow lead in the polls, new leads in a case, electrical or ECG leads, a dog's lead) more often than the verb, and the verb in less usual frames
1	lead.metal	lead	lead|leads	15	the metal: exposure and health (exposed to lead, levels of lead, lead poisoning), plumbing and materials (lead pipes, lead service lines, lead paint, lead weights, pencil lead), including rows with a verb or to right before the target
1	close.adj-noun	close	close|closer	15	final s sound: the adjective after linking verbs and degree adverbs, in comparisons (closer), close to, a close friend, a close call, a close race, and the noun for an enclosed precinct (the cathedral close)
1	close.verb	close	close|closer	15	final z sound: the verb with objects and particles (close the gate, close down, will close at noon), the noun meaning the end (at the close of trading), and closer as one who closes (the team's closer)
1	live.adj	live	live	15	rhymes with five: broadcast and performance (live music, broadcast live, went live, a live recording), electricity (a live wire), living things (live bait, live animals), with the target after a verb in some rows
1	live.verb	live	live	15	rhymes with give: live in, live with, live on, live for, after modals and to, imperatives, questions (where do you live)
1	wind.air	wind	wind|winds	15	moving air or breath: weather, sailing, breath (get your wind back), wind instruments and compounds (wind turbine, wind chill), the target after a verb in some rows (felt wind, caught the wind)
1	wind.coil	wind	wind|winds	15	to coil or turn: wind the clock, watch, cable, yarn or film, wind down, a road or river that winds, instructions with an adverb (wind it fully, wind slowly)
1	content.noun	content	content|contents	15	what is contained: digital and media content (web content, content type, user content), the content or contents of a letter, box or report, fat, salt or moisture content
1	content.satisfied	content	content|contents	15	satisfied: after linking verbs (seemed content, was content), content with, content to wait, the verb (content oneself with; he contents himself), to her heart's content
```

## Prompt text, part 0

```text
PART=0 of 2. You are writing SYNTHETIC TRAINING SENTENCES for an English heteronym pronunciation classifier: for
one target word in each sentence, the classifier decides which pronunciation (reading) applies. Your rows become
training data for readings that the classifier still confuses in context, so they must be correct, natural and varied.

WORKSPACE. Work ONLY in <STAGE>. Allowed reads: <STAGE>\readings.tsv (reading_id, lemma, pos_hint, description,
status; the descriptions say how each reading is pronounced), <STAGE>\forms.tsv (form, reading_id, kind: the surface
forms of each reading), <STAGE>\quotas-v4.tsv (the quotas), <STAGE>\part-0-prompt.md (this prompt), and
<STAGE>\train.tsv and <STAGE>\dev.tsv ONLY to avoid duplicating their sentences and templates (never copy, paraphrase or
imitate their sentences). Write ONLY files whose names start with part-0- in <STAGE> (the output, the validator, the
notes and any scratch file). FORBIDDEN: any other file or folder on disk (including any git repository, any other data
file, any test or evaluation set and any file of the other part or of a review), web search, and any model or
evaluation output. Write every sentence yourself, originally. The TSV files are tab-separated with NO quoting: parse
them by splitting lines on the tab character (Python: csv.reader(f, delimiter='\t', quoting=csv.QUOTE_NONE)).

YOUR SHARE. The lines of quotas-v4.tsv whose part column is 0: the lemmas tear, wound (its wind.coil rows use the form wound, the past tense of wind), minute and read; 285 rows in total. Each line
gives a reading_id, its lemma, the allowed target forms (separated by "|"), a number of rows and a focus. Write exactly
that number of rows for every line, each using the target in exactly that reading and following the line's focus.
Before you write a lemma, re-read the descriptions of ALL readings of the lemma in readings.tsv: the pronunciation is
what decides the reading.

WHAT THE ROWS MUST TEACH. The classifier fails when the words that decide the reading are not the most common ones, and
when both readings occur in similar frames. Therefore:
- Contrastive pairs: for each lemma, write at least a third of the rows as contrastive pairs: two rows of DIFFERENT
  readings of the lemma that share the words right around the target or a cue word, so that a small change carries the
  difference (for example "a tear on her cheek" against "a tear in his calf"; "the deep wound" against "the tightly
  wound spring"; "did not read it yesterday" against "read it yesterday"). Each row of a pair counts toward its own
  reading's quota line.
- Balance: give the readings of a lemma the same kinds of frames (the same determiners, prepositions, sentence positions
  and registers), so that no surface pattern other than the deciding words gives the reading away.
- Variety: vary the cue words widely; use any one cue word (a content word that decides the reading) in at most 3 rows
  of a reading. Vary register (news, technical, medical, sport, fiction, conversation, instructions, headlines written
  as sentences, questions), sentence length, and the position of the target.

EVERY ROW.
- One sentence of 5 to 35 words (count runs of letters and digits), plain ASCII only (straight quotes and apostrophes,
  no tab, no other control character), ending with sentence punctuation.
- The target is one whole word whose lower-case form is an allowed target form of the quota line and is listed in
  forms.tsv for the row's reading_id; it is not joined to another word by a hyphen or an apostrophe. It occurs exactly
  once: no form that forms.tsv lists for any reading of the lemma occurs elsewhere in the sentence, and for the wind,
  winds and wound targets none of the words wind, winds, winding, wound, wounds, wounded occurs besides the target.
- A careful human reader can tell the reading from the sentence alone.
- The phrases quoted in this prompt and in the quota file are illustrations: never use one of them as a whole
  sentence.
- Original text: no quotation, no lyrics, no famous lines, no copyrighted text, no real living person; neutral content.
- No duplicate: no two rows share a sentence, no row repeats a sentence of train.tsv or dev.tsv (compare the lower-cased
  letters and digits only), and do not write near copies of your own rows or of those files.
- No used template. The template of a row is computed from its sentence and target span: words = the runs of the
  characters [a-z0-9'] in the lower-cased text; template = " ".join(words before the target, last 2) + " _ " +
  " ".join(words after the target, first 2). A row's (reading_id, template) pair must not occur in train.tsv or dev.tsv
  (compute their templates the same way from their sentence, start and end columns). A template may repeat within your
  own rows, but prefer variety.

OUTPUT. Write <STAGE>\part-0-output.tsv: UTF-8 without byte order mark, LF line endings, a final newline, tab
separated, no quoting, with exactly this header line:

id	lemma	reading_id	sentence	start	end	source	license	split	batch

- id: syn4-0-1, syn4-0-2, ... in file order;
- lemma: the lemma column of readings.tsv for reading_id (for example "wind" for wind.coil, also when the target is
  "wound");
- start, end: the character offsets of the target in the sentence, end exclusive (Python sentence[start:end] is the
  target);
- source: synthetic-v4; license: CC0-1.0; split: train (the development rows are chosen later, not by you);
- batch: syn4-0-<reading_id>-b<j> (j = 1, 2, ... per reading). Write in batches of 5 to 10 rows of one reading and
  append each batch to the file, so that progress is not lost.

SELF-VALIDATION. Write <STAGE>\part-0-validate.py, taking the output file as its argument, and run it; fix every
problem it reports before you finish. It must check: the exact header; ids in order without gaps; that reading_id
exists in readings.tsv with the given lemma and is a part-0 reading of quotas-v4.tsv; that sentence[start:end].lower()
is an allowed form of a part-0 quota line of the reading and is listed in forms.tsv for reading_id, and that the
characters just before and after the span are not letters, digits, hyphens or apostrophes; exactly one occurrence of
the lemma's forms as described above; 5 to 35 words; ASCII only and no tab; the source, license and split values; the
batch pattern, one reading per batch and 5 to 10 rows per batch; that the row count of every reading equals the sum of
its quota lines; no duplicate sentence within the file or against train.tsv and dev.tsv; and no (reading_id, template)
pair of train.tsv or dev.tsv.

Also write <STAGE>\part-0-notes.md with your model name, the times you started and finished (UTC), the number of
contrastive pairs per lemma and the validator's final output. Do not explain your labels anywhere else.

Return ONLY: rows written per reading, contrastive pairs per lemma, and the validator result.

TOP-UP ROUNDS. Some rows may later be removed by checks against data you cannot see, or by a blind review. A new writer
may then receive this prompt with a first line "TOP-UP ROUND <r>: write <n> more rows for <reading_id> (quota lines
...); some earlier rows were not usable." That writer follows every rule above for the named readings and counts only,
reads <STAGE>\part-0-output.tsv and any earlier <STAGE>\part-0-top-up-*.tsv only to avoid duplicating them, and
writes <STAGE>\part-0-top-up-<r>.tsv (same header; ids syn4-0-t<r>-1, syn4-0-t<r>-2, ...; batches
syn4-0-<reading_id>-t<r>-b<j>, 5 to 10 rows each, or all of a reading's rows when fewer than 5 are asked), validated
with the same checks (the counts against the requested numbers instead of the quota lines), and notes in
<STAGE>\part-0-top-up-<r>-notes.md.
```

## Prompt text, part 1

```text
PART=1 of 2. You are writing SYNTHETIC TRAINING SENTENCES for an English heteronym pronunciation classifier: for
one target word in each sentence, the classifier decides which pronunciation (reading) applies. Your rows become
training data for readings that the classifier still confuses in context, so they must be correct, natural and varied.

WORKSPACE. Work ONLY in <STAGE>. Allowed reads: <STAGE>\readings.tsv (reading_id, lemma, pos_hint, description,
status; the descriptions say how each reading is pronounced), <STAGE>\forms.tsv (form, reading_id, kind: the surface
forms of each reading), <STAGE>\quotas-v4.tsv (the quotas), <STAGE>\part-1-prompt.md (this prompt), and
<STAGE>\train.tsv and <STAGE>\dev.tsv ONLY to avoid duplicating their sentences and templates (never copy, paraphrase or
imitate their sentences). Write ONLY files whose names start with part-1- in <STAGE> (the output, the validator, the
notes and any scratch file). FORBIDDEN: any other file or folder on disk (including any git repository, any other data
file, any test or evaluation set and any file of the other part or of a review), web search, and any model or
evaluation output. Write every sentence yourself, originally. The TSV files are tab-separated with NO quoting: parse
them by splitting lines on the tab character (Python: csv.reader(f, delimiter='\t', quoting=csv.QUOTE_NONE)).

YOUR SHARE. The lines of quotas-v4.tsv whose part column is 1: the lemmas bass, lead, close, live, wind (the forms wind and winds) and content; 180 rows in total. Each line
gives a reading_id, its lemma, the allowed target forms (separated by "|"), a number of rows and a focus. Write exactly
that number of rows for every line, each using the target in exactly that reading and following the line's focus.
Before you write a lemma, re-read the descriptions of ALL readings of the lemma in readings.tsv: the pronunciation is
what decides the reading.

WHAT THE ROWS MUST TEACH. The classifier fails when the words that decide the reading are not the most common ones, and
when both readings occur in similar frames. Therefore:
- Contrastive pairs: for each lemma, write at least a third of the rows as contrastive pairs: two rows of DIFFERENT
  readings of the lemma that share the words right around the target or a cue word, so that a small change carries the
  difference (for example "a tear on her cheek" against "a tear in his calf"; "the deep wound" against "the tightly
  wound spring"; "did not read it yesterday" against "read it yesterday"). Each row of a pair counts toward its own
  reading's quota line.
- Balance: give the readings of a lemma the same kinds of frames (the same determiners, prepositions, sentence positions
  and registers), so that no surface pattern other than the deciding words gives the reading away.
- Variety: vary the cue words widely; use any one cue word (a content word that decides the reading) in at most 3 rows
  of a reading. Vary register (news, technical, medical, sport, fiction, conversation, instructions, headlines written
  as sentences, questions), sentence length, and the position of the target.

EVERY ROW.
- One sentence of 5 to 35 words (count runs of letters and digits), plain ASCII only (straight quotes and apostrophes,
  no tab, no other control character), ending with sentence punctuation.
- The target is one whole word whose lower-case form is an allowed target form of the quota line and is listed in
  forms.tsv for the row's reading_id; it is not joined to another word by a hyphen or an apostrophe. It occurs exactly
  once: no form that forms.tsv lists for any reading of the lemma occurs elsewhere in the sentence, and for the wind,
  winds and wound targets none of the words wind, winds, winding, wound, wounds, wounded occurs besides the target.
- A careful human reader can tell the reading from the sentence alone.
- The phrases quoted in this prompt and in the quota file are illustrations: never use one of them as a whole
  sentence.
- Original text: no quotation, no lyrics, no famous lines, no copyrighted text, no real living person; neutral content.
- No duplicate: no two rows share a sentence, no row repeats a sentence of train.tsv or dev.tsv (compare the lower-cased
  letters and digits only), and do not write near copies of your own rows or of those files.
- No used template. The template of a row is computed from its sentence and target span: words = the runs of the
  characters [a-z0-9'] in the lower-cased text; template = " ".join(words before the target, last 2) + " _ " +
  " ".join(words after the target, first 2). A row's (reading_id, template) pair must not occur in train.tsv or dev.tsv
  (compute their templates the same way from their sentence, start and end columns). A template may repeat within your
  own rows, but prefer variety.

OUTPUT. Write <STAGE>\part-1-output.tsv: UTF-8 without byte order mark, LF line endings, a final newline, tab
separated, no quoting, with exactly this header line:

id	lemma	reading_id	sentence	start	end	source	license	split	batch

- id: syn4-1-1, syn4-1-2, ... in file order;
- lemma: the lemma column of readings.tsv for reading_id (for example "wind" for wind.coil, also when the target is
  "wound");
- start, end: the character offsets of the target in the sentence, end exclusive (Python sentence[start:end] is the
  target);
- source: synthetic-v4; license: CC0-1.0; split: train (the development rows are chosen later, not by you);
- batch: syn4-1-<reading_id>-b<j> (j = 1, 2, ... per reading). Write in batches of 5 to 10 rows of one reading and
  append each batch to the file, so that progress is not lost.

SELF-VALIDATION. Write <STAGE>\part-1-validate.py, taking the output file as its argument, and run it; fix every
problem it reports before you finish. It must check: the exact header; ids in order without gaps; that reading_id
exists in readings.tsv with the given lemma and is a part-1 reading of quotas-v4.tsv; that sentence[start:end].lower()
is an allowed form of a part-1 quota line of the reading and is listed in forms.tsv for reading_id, and that the
characters just before and after the span are not letters, digits, hyphens or apostrophes; exactly one occurrence of
the lemma's forms as described above; 5 to 35 words; ASCII only and no tab; the source, license and split values; the
batch pattern, one reading per batch and 5 to 10 rows per batch; that the row count of every reading equals the sum of
its quota lines; no duplicate sentence within the file or against train.tsv and dev.tsv; and no (reading_id, template)
pair of train.tsv or dev.tsv.

Also write <STAGE>\part-1-notes.md with your model name, the times you started and finished (UTC), the number of
contrastive pairs per lemma and the validator's final output. Do not explain your labels anywhere else.

Return ONLY: rows written per reading, contrastive pairs per lemma, and the validator result.

TOP-UP ROUNDS. Some rows may later be removed by checks against data you cannot see, or by a blind review. A new writer
may then receive this prompt with a first line "TOP-UP ROUND <r>: write <n> more rows for <reading_id> (quota lines
...); some earlier rows were not usable." That writer follows every rule above for the named readings and counts only,
reads <STAGE>\part-1-output.tsv and any earlier <STAGE>\part-1-top-up-*.tsv only to avoid duplicating them, and
writes <STAGE>\part-1-top-up-<r>.tsv (same header; ids syn4-1-t<r>-1, syn4-1-t<r>-2, ...; batches
syn4-1-<reading_id>-t<r>-b<j>, 5 to 10 rows each, or all of a reading's rows when fewer than 5 are asked), validated
with the same checks (the counts against the requested numbers instead of the quota lines), and notes in
<STAGE>\part-1-top-up-<r>-notes.md.
```
