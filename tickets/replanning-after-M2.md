# Replanning after M2 — draft

**Status (2026-09-28):** R6–R9, R11 and R12 **approved by the author and written into `SPEC.md`**
(§5, §6, §10, §11, §15, §16, decision 17); README updated to the M2 state.
**R10 is deferred** until demo step 2 has run against real Azure OpenAI. M3 tickets are cut only after that.

## 1. What was learned

### Deviations recorded in the tickets

| Ticket | Deviation |
|---|---|
| M2-01 | `RealAiClientsApiFactory` derives from `CloudKnowledgeApiFactory` and only switches the fakes off. |
| M2-02 | 67 chunks instead of the estimated ~40–55. `Application` references `Microsoft.Extensions.Options`. Repeated headings get a numbered slug. |
| M2-03 | Every test host gets its own temp cache file. "Unreadable cache" means invalid JSON only; I/O errors still throw. |
| M2-04 | `Application` references `Microsoft.Extensions.Logging.Abstractions`. The M1 config-validation test was rewritten (race in `WebApplicationFactory`). |
| M2-05 | `POST /index` runs with the app's stopping token, not the request's. |
| M2-06 | `VectorData.Abstractions` pinned to 10.1.0 (connector incompatibility). The threshold test uses `MinScore = 0.15` because the fake scores are low. |

### Surprises

1. **The preview connector does not work with the newest stable abstraction.** `Connectors.InMemory` 1.74.0-preview
   (newest) is built against `VectorData.Abstractions` 10.1.0. With 10.10.0 the solution compiles and upserts work, but
   every vector search throws `TypeLoadException` (`VectorSearchFilter` was removed). The M1 package check verified
   "stable vs. preview", not compatibility. It surfaced only at runtime, in the first search test.
2. **No section is split.** With `MaxTokens = 400` all 66 H2 sections fit (39–395 estimated tokens, median 129), plus one
   introduction → 67 chunks = sections. Citations will therefore always point to exactly one section. The splitting
   logic stays (covered by unit tests) but has no effect on the current knowledge base.
3. **Bag-of-words scores are low.** The best fake hit for a matching query is ≈ 0.18, because whole sections share only a
   few words with a short query. Rankings are meaningful, absolute values are not — a problem for `MinScore` tests in M3.
4. **`WebApplicationFactory` races when the app throws during startup** (`ObjectDisposedException` instead of the real
   exception, ~1 in 3 runs once more tests run in parallel).

### Easier / harder than expected

- Easier: chunker, cache and indexer were straightforward; with the cache a re-index is instant.
- Harder: test-host timing. Startup indexing runs in the background, so every test that drives `IndexState` or index
  runs itself must disable it; the real-clients factory must never index (it would call Azure).

### Environment

- `dotnet run` takes the port from `launchSettings.json` (5210) and ignores `ASPNETCORE_URLS`.
- Starting the built executable directly uses the *working directory* as content root → `appsettings.json` is not found
  and startup fails fast. Relevant for `CloudKnowledge.Eval` in M4, which also runs from `bin/`.
- The cloud container still has no .NET SDK preinstalled (SessionStart hook still undecided).

### Still unknown — needs real Azure

- The real score range of `text-embedding-3-small` for relevant vs. irrelevant sections. M3 needs a provisional
  `MinScore`, and the only source for it before M4's evaluation is demo step 2 (and the two extra searches in the `.http` file).

## 2. Proposed SPEC updates (need approval)

| # | Section | Proposal | Reason |
|---|---|---|---|
| R6 | §6 (constitution), decision 15 | Replace "`VectorData.Abstractions` is stable (10.10.0)" with: "`VectorData.Abstractions` **10.1.0** is pinned — the version the InMemory connector 1.74.0-preview is built against. 10.10.0 removed types the connector uses (runtime `TypeLoadException` on search). Upgrade abstraction and connector only together." Add decision 17: *Pin the abstraction to the connector's version* — alternative: own cosine store — reason: keeps the stable abstraction and the swappable connector; the search tests catch a mismatch. | Surprise 1. §6 currently states a version that does not work. |
| R7 | §5 / §6 (constitution) | State which packages `Application` may reference: `Microsoft.Extensions.AI.Abstractions`, `Microsoft.Extensions.VectorData.Abstractions`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Logging.Abstractions` — abstractions only, never Azure/OpenAI/Semantic Kernel (checked by a test since M2-04). | Makes the dependency rule checkable; documents M2-02/M2-04 deviations. |
| R8 | §16 | Add test-host rules: (a) tests that drive `IndexState` or index runs set `Knowledge:IndexOnStartup=false`; (b) the real-clients factory never indexes on startup; (c) every factory uses its own temp embedding cache; (d) configuration-validation tests call `IStartupValidator.Validate()` instead of starting a host that throws; (e) bag-of-words scores are only valid for **rankings** — `RagService` tests (M3) control scores explicitly (scripted vectors or a fake search result), not via bag-of-words magnitudes. | Surprises 3 and 4; keeps M3 tests deterministic. |
| R9 | §10 | `RagService` reuses `KnowledgeSearch`: the `/ask` retrieval is exactly the `/search` retrieval, and the `MinScore` guard is "keep hits with `aboveThreshold = true`". One retrieval path, one place for the threshold. | Avoids a second search implementation; makes the talking point "retrieval ≠ generation" visible in the code. |
| R10 | §0 (M3 row) | "Provisional `MinScore`" = derived from the real scores of demo step 2 and the two extra `.http` searches (e.g. just below the lowest relevant top-3 score), set in `appsettings.json` at the start of M3 and replaced by the calibrated value in M4. | Surprise "still unknown"; M3's done-when needs a concrete value. |
| R11 | §15 | `CloudKnowledge.Eval` sets its content root / configuration base path to `AppContext.BaseDirectory` and copies `appsettings.json` and `docs/` into its output. It uses its own embedding cache (first eval run embeds the 67 chunks once — negligible cost) unless an absolute `EmbeddingCachePath` is configured. | Environment finding: running from `bin/` without this fails at startup. |
| R12 | §11 | Update the `POST /index` example to the real knowledge base (`documents: 9`, `chunks: 67`). | Cosmetic; the example should match what the demo shows. |

Roadmap (§0 milestone table): **unchanged** — M3 and M4 stay as planned.

## 3. Remaining steps

1. ✅ Write the approved R6–R9, R11, R12 into `SPEC.md`.
2. ✅ Update the README's current architecture (section 2) and the Ist/Ziel table to the M2 state.
3. ⏳ Author runs demo step 2 against real Azure OpenAI → record top-5 scores in the M2 validation log,
   tick the last M2-06 criterion.
4. ⏳ Decide R10 (provisional `MinScore` for M3) from those scores and write it into SPEC §0 and `appsettings.json`.
5. ⏳ Complete the M2 row in the replanning log, then cut the M3 tickets.
