# Handoff Notes for the Next Engineer — DedupTool (INGEST-4471)

Purpose: let any engineer pick this project up where it was left, without the original conversation.
Written with AI assistance and reviewed by the developer. Companion to `DECISIONS.md` (decisions) and `AI_Log.md` (how the AI was used).

## 1. What this is, in one minute

A .NET 8 CLI that reads a batch JSON (`documents[]`) and writes a CSV load file for the review platform, removing duplicate content without breaking families.

**The bug (INGEST-4471):** the old tool kept one global set of sha256 values and dropped any document whose hash was already seen, attachments included. A shared attachment was removed from every family but one, so reviewers saw emails missing attachments.

**The fix:** deduplicate by family. An attachment is suppressed only together with its whole family.

## 2. How it works now

Pipeline in `Deduper.Run`: `BuildIndex` -> `ResolveRoots` -> `DecideDuplicateRoots` -> rows (sorted by DocID).

- Loose files: same `sha256` = duplicate (§3.1).
- Emails: same `EmailIdentity.Key` = duplicate (§3.3). Key = lower-cased sender, sorted set of recipients, trimmed subject (`Re:`/`FW:` kept), sent instant in UTC, body normalised (CRLF->LF, trim), sorted hashes of descendants; fields length-prefixed, then SHA-256.
- Survivor of a duplicate group = lowest `DocId` (ordinal). Output sorted by `DocID` (§6.1).
- Empty hash = not comparable: kept, with a warning (key `U:<DocId>`).
- Cycle, missing parent or repeated `docId` -> `BatchValidationException`, exit 1, no new CSV.
- Exit codes: 0 ok, 1 invalid batch, 2 usage, 3 unexpected (including out of memory).
- CSV: RFC 4180, LF, UTF-8 no BOM, written to `.tmp` then renamed. Dates UTC `YYYY-MM-DDTHH:MM:SSZ`.

Files: `src/DedupTool/{Program,DedupCli,Deduper,EmailIdentity,UtcDate,LoadFileWriter,BatchValidationException,Log,Models}.cs`. Tests: `test/DedupTool.Tests/` by feature folder (86 tests), golden file `Data/sample-expected.csv`.

Run: `dotnet test`, then run the CLI on `data/batch-sample.json` writing the output **outside** `data/`.

## 3. Global deduplication today (and what that costs)

The tool deduplicates **globally** (§3.4). Consequence: if Alvin and Simon hold the same document, one copy survives and `Custodian` shows the holder of the survivor. In the sample, `mlee` loses DOC-000009, 000010, 000011 and 000013 to other holders.

This **does not satisfy §4** (each custodian's set complete on its own). §3.4 and §4 contradict each other under a fixed 8-column file; global was chosen because it keeps the explicit dedup requirement and the fixed contract. Open product question for the client.

Practical effect: if the holder of the surviving copy is severed from the matter, the other custodians lose that document.

## 4. Backlog (proposals, in suggested order)

| # | Item | Why | Needs the client? |
|---|------|-----|-------------------|
| 1 | **Duplicate map** ("who else had this document"), as an extra file or column | Closes §4 and severance without changing global dedup | Yes: the column order/header is fixed and the ingest job breaks if it changes |
| 2 | **Parameterise the dedup scope** (`global` default, `custodian` optional) | Lets the client choose §3.4 or §4 behaviour | Yes, product decision |
| 3 | Uniform failure policy | Today a missing `documents` array fails, an unreadable date only warns, `parentId:""` stops the batch | Yes: which errors stop, which warn |
| 4 | Lower memory: incremental JSON read, drop bodies after hashing | 2M docs needed 6.0 GB (~3 KB/doc). ~0.5 KB/doc is an estimate, not measured | No |
| 5 | Strict ISO-8601 date parser | `TryParse` accepts culture formats (read as MM/dd) and truncates fractions (real tolerance 1 s) | No |
| 6 | Email key with tree shape; children in the key of loose files; CC/BCC | Closes the known false-positive paths below | CC/BCC: needs data that carries them |
| 7 | Parallelise by family root | Throughput; stays deterministic because survivor = lowest `DocId` | No |
| 8 | Load test with a synthetic generator inside the repo | The 2M figures came from a generator not in the project | No |
| 9 | Near-duplicates, Unicode normalisation, nested emails compared by content | Not requested | Probably |

### On parameterising global vs custodian (recommended by the AI, to be decided by the engineer/developer)

Do it as **one algorithm with a scope parameter**, not a second algorithm: in `custodian` scope the dedup key (loose hash or `EmailIdentity.Key`) is prefixed with the custodian, so duplicates are only found inside one mailbox. The family-first logic, the writer and the contract stay identical. Risks: it doubles the test surface (every dedup test needs both scopes), and `custodian` scope means the same email in two mailboxes is **not** deduplicated, which weakens §3.3/§3.4. Default must stay `global`. The duplicate map (item 1) is the better answer if the client accepts a new file, because it satisfies both requirements at once.

## 5. Known limits and possible bugs (documented, not fixed: scope discipline)

- Email key uses the **set** of descendant hashes, not the tree shape: two emails with the same attachments organised differently would collapse (one family lost). Rare.
- A loose file ignores its children when compared.
- No CC/BCC in the model or the key: two emails differing only in CC/BCC would collapse.
- Dates without offset are read as UTC. Culture formats read as MM/dd. Fractions truncated.
- Previous CSV survives a failed run (by design, atomic write); a stale file can be mistaken for fresh output.
- Out-of-memory message (exit 3) has no automated test.
- Only measured on synthetic data, Ryzen 7 5800H, 15.9 GB, one run per size: 100k docs 2.1 s / 345 MB; 500k 6.4 s / 1.5 GB; 2M 34.6 s / 6.0 GB. Time is linear; memory is the limit (free RAM >= ~3x the JSON).

## 6. Why empty hash = "not comparable"

The spec says `sha256` is present on every document, so an empty one is malformed input. Plausible causes: the file could not be read during collection (locked, corrupt, access denied), the hash step failed or was skipped by the export tool, or the field was dropped when the batch was assembled. We cannot prove such a document is a duplicate of another, and grouping all empty hashes together would delete unrelated files. So: keep it, warn with the DocID. An email with an unhashed attachment is also kept (otherwise two emails with different unhashed attachments could look equal). Alternatives (fail the whole batch, or recompute the hash from the file) need the client: the tool never sees the raw bytes.

## 7. Principle behind most decisions

A false positive (suppressing something that was not a duplicate) loses evidence; a false negative only costs reading time. When in doubt, keep.

## 8. Do not touch without agreement

`README.md`, `SPEC.md`, `data/`, the 8-column header and order. Do not rewrite `DECISIONS.md` for the developer.

## 9. Where to read more

`DECISIONS.md` (summary), `AI_Log.md` (condensed AI log; the original, longer log is kept by the developer), test names (each states the rule it protects).
