# M2-01 — Test host fakes and knowledge paths

**Milestone:** M2 · **Blocked by:** — · **Spec:** §12 (paths, decision 12), §16 (test host, decision 14)

## Goal

Prepare the ground for indexing: the API finds `docs/` no matter how it is started, and no integration test can
reach Azure once indexing runs on startup.

## What to build

**Knowledge paths (R1)**
- `CloudKnowledge.Api.csproj` copies `../../docs/*.md` into the build output as `docs/*.md`
  (`<Content Include=... LinkBase="docs" CopyToOutputDirectory="PreserveNewest" />` or equivalent).
- `KnowledgeOptions` gets `IndexOnStartup` (`bool`, default `true`) and `appsettings.json` lists it.
- A small resolver (Infrastructure), e.g. `KnowledgePaths.Resolve(string path)`: relative paths are combined with
  `AppContext.BaseDirectory`, absolute paths are returned unchanged. Used for `DocsPath` and `EmbeddingCachePath`.

**Test host (R3)**
- `FakeEmbeddingGenerator.BagOfWords()`: lower-cases a text, splits it into words, hashes each word into one of
  1536 dimensions and L2-normalises the vector. Deterministic; texts sharing words get similar vectors.
- `CloudKnowledgeApiFactory` registers `FakeEmbeddingGenerator.BagOfWords()` and a `FakeChatClient` **by default**
  (via `ConfigureTestServices`) and exposes them as properties so tests can inspect calls.
- A separate factory (e.g. `RealAiClientsApiFactory`) without fakes, used only by the DI-registration tests from
  M1-02. Those tests never call the clients.

## Not in this ticket

- Reading or chunking documents (M2-02, M2-03). Only the files and the path resolver.
- The indexing hosted service (M2-05).

## Acceptance criteria

- [x] After `dotnet build`, the Api output folder and the test output folder both contain `docs/` with the nine files.
- [x] Unit tests for the resolver: relative → below `AppContext.BaseDirectory`; absolute → unchanged.
- [x] Integration test: resolved `DocsPath` of the test host contains exactly the nine `.md` files.
- [x] Unit test: `BagOfWords()` returns 1536 dimensions, unit length, the same vector for the same text, and a higher
      cosine similarity for "HTTP 503 after deployment" vs. "503 deployment" than vs. "upload blob storage".
- [x] Default `CloudKnowledgeApiFactory` resolves the fakes; the M1-02 registration tests still pass using the real-clients factory.
- [x] `IndexOnStartup` is bound and defaults to `true`.

## Implementation notes

- `RealAiClientsApiFactory` derives from `CloudKnowledgeApiFactory` and only switches the fakes off
  (`UseFakeAiClients => false`), so both share settings and `WithSetting`.
- `BagOfWords()` splits at non-letter/non-digit characters and hashes words with FNV-1a, because
  `string.GetHashCode()` is randomised per process. `BagOfWordsVector(text)` is public so tests can compare vectors directly.
- The docs reach the test output transitively through the project reference to the Api; no extra item in the test project.
