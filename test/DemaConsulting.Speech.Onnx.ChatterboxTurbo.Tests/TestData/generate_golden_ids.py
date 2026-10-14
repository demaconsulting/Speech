"""Generates chatterbox-turbo-tokenizer-golden.json: reference token ids from the real Hugging Face tokenizer.

Provenance: the ids are produced by transformers' AutoTokenizer for ResembleAI/chatterbox-turbo-ONNX
(tokenizer.json, GPT-2 byte-level BPE) using the default call tok(text) - i.e. add_special_tokens=True,
which applies the tokenizer's TemplateProcessing post-processor and therefore appends <|endoftext|>
(50256) twice. That is exactly what the reference inference pipeline embeds.

Usage (the large ONNX graphs are NOT needed - only the tokenizer files from the model repository):

    python generate_golden_ids.py <directory containing tokenizer.json> chatterbox-turbo-tokenizer-golden.json

The unit tests read the committed JSON; they never need this script, Python, or the model.
"""

import json
import sys

from transformers import AutoTokenizer

# Representative corpus: plain text, punctuation, em dash, ellipsis, digits, unicode (accents, CJK, emoji),
# whitespace oddities, every paralinguistic/emotion special token and the end-of-text token.
SPECIAL_TOKENS = [
    "[angry]", "[fear]", "[surprised]", "[whispering]", "[advertisement]", "[dramatic]",
    "[narration]", "[crying]", "[happy]", "[sarcastic]", "[clear throat]", "[sigh]", "[shush]",
    "[cough]", "[groan]", "[sniff]", "[gasp]", "[chuckle]", "[laugh]", "<|endoftext|>",
]

CORPUS = [
    "Hello world.",
    "The quick brown fox jumps over the lazy dog.",
    "Oh, that's hilarious! [chuckle] Um anyway, how are you doing today?",
    "So funny [laugh] it.",
    "Wait... what? No way!",
    "It was a long night \u2014 and a longer morning.",
    "Wait\u2026 really?",
    "I have 3 apples and 12 oranges; that's 15 in total (roughly).",
    "caf\u00e9 na\u00efve fa\u00e7ade \u00fcber",
    "\u4f60\u597d\uff0c\u4e16\u754c",
    "Smile \U0001f600 please",
    "  leading and trailing   spaces  ",
    "Line one.\nLine two.\tTabbed.",
    "[laugh]",
    "[laugh] That is funny.",
    "Before [clear throat] after.",
    "A [cough] B [gasp] C [sigh].",
    "don't won't can't I'll we've she'd",
    "",
] + SPECIAL_TOKENS


def main() -> None:
    model_dir, output_path = sys.argv[1], sys.argv[2]
    tokenizer = AutoTokenizer.from_pretrained(model_dir)
    cases = []
    for text in CORPUS:
        ids = tokenizer(text)["input_ids"]
        cases.append({"text": text, "ids": [int(i) for i in ids]})
    with open(output_path, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(cases, handle, ensure_ascii=True, indent=1)
        handle.write("\n")


if __name__ == "__main__":
    main()
