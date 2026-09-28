# M2-02 — Markdown chunker

**Milestone:** M2 · **Blocked by:** — · **Spec:** §8, §16 (chunker tests)

## Goal

Turn a Markdown document into section-sized chunks — the unit that is embedded, searched and cited.
Pure logic, no I/O, fully unit-tested.

## What to build

In `Application/Chunking`:

```csharp
public sealed record MarkdownDocument(string Name, string Content);   // Name = "troubleshooting.md"

public sealed record DocumentChunk(
    string Id,             // "{document}#{sectionSlug}-{partIndex}", e.g. "troubleshooting.md#http-503-after-deployment-0"
    string DocumentName,
    string DocumentTitle,  // H1, or file name without extension if there is no H1
    string Section,        // H2 text, or "Introduction"
    string Content,        // raw section text without the heading
    string TextToEmbed);   // "{DocumentTitle} > {Section}\n\n{Content}"

public sealed class MarkdownChunker(IOptions<ChunkingOptions> options)
{
    public IReadOnlyList<DocumentChunk> Chunk(MarkdownDocument document);
}
```

Rules exactly as SPEC §8:
1. Split at `## ` headings (only outside fenced code blocks). Text between H1 and the first H2 → section
   "Introduction"; skipped if it is empty.
2. Section ≤ `MaxTokens` → one chunk. Token estimate = `ceil(chars / 4)`.
3. Longer section → split at blank lines into parts ≤ `MaxTokens`. Fenced code blocks and lists stay in one piece.
   A single block larger than `MaxTokens` stays whole.
4. No overlap. Content is trimmed. `partIndex` starts at 0 per section.
5. Section slug: lower-case, non-alphanumerics → `-`, collapsed and trimmed.

Register `MarkdownChunker` as a singleton in `AddCloudKnowledge()`.

## Not in this ticket

- Reading files (M2-03), embedding, storing.
- Any tokenizer package.

## Acceptance criteria

- [x] Unit tests: H2 splitting; intro section present/absent; `## ` inside a code block is not a heading;
      long section split at blank lines; code block and list never split; oversized single block stays whole;
      `TextToEmbed` format; stable, unique ids; slug rules.
- [x] Test against the real `docs/`: every document yields at least three chunks; all ids are unique across the
      knowledge base; `troubleshooting.md` has a chunk whose `Section` is `HTTP 503 after deployment`;
      no chunk exceeds `MaxTokens` unless it is a single oversized block.
- [x] The chunk count for the real `docs/` with the default `MaxTokens` is written into the ticket (expected ~40–55).
      **Result: 67 chunks** — see implementation notes.

## Implementation notes

- **Chunk count 67, not ~40–55.** The knowledge base has 66 H2 sections plus one introduction
  (`troubleshooting.md`). With `MaxTokens = 400` no section is split: section sizes are 39–395 estimated tokens
  (median 129). The ticket's estimate was simply too low; the rules are unchanged. Splitting is covered by the
  unit tests with `MaxTokens = 50`.
- `Application` now references `Microsoft.Extensions.Options` (10.0.12) for `IOptions<ChunkingOptions>` —
  an abstraction package, no Azure dependency.
- Repeated H2 headings in one document get a numbered slug (`notes`, `notes-2`) so ids stay unique.
  Does not occur in the real docs.
- H2 sections with no content are skipped, like an empty introduction (nothing to embed).
- A list is detected by its first line (`-`, `*`, `+`, `1.`, `1)`); following list items and indented
  continuation blocks are kept with it. Fences are ```` ``` ```` or `~~~`.
- `EstimateTokens` and `Slugify` are public static so tests (and later the indexer log) can use them.
