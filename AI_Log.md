# AI Log — DedupTool

Written by the assistant, **Kosmos AI**, as a record of how the engineer and I worked on this
exercise: the method, the rounds, the decisions, what the engineer checked and pushed back on.
It is a condensed account, not a chat transcript. The full, long chronological version of this
log (16 entries) was kept and can be provided on request.

## 1. Who worked on this

- **The engineer** directed the work, took every decision, wrote `DECISIONS.md` by hand
  (I never touched it), and reviewed each round.
- **Kosmos AI** (me) is an agent the engineer built as his own tool for software development. It runs
  on Claude and carries its own rules: read the knowledge base before acting, keep a handoff updated
  at the end of every session, ask for an explicit go-ahead before writing in a client folder, and
  never commit or push. Its notes live outside this repository; only this log was written in it by
  me besides code and tests.
- **Other AI sessions** were used by the engineer as independent reviewers (section 6).

## 2. Summary

This was not "read this and solve it". The engineer asked first for understanding, then for
evidence, then for a plan, and only then for code. The project was built in small steps and rounds
of review, and each step was checked against the six README tasks and the spec before moving on:

1. **Understand and prove the bug** before touching anything (analysis, instrumented run).
2. **Settle strategy**: what cannot change, what we may decide, where the spec contradicts itself.
3. **Learn the domain** (e-discovery: families, custodians, global dedup) and fix the scope.
4. **Build in rounds R1–R8**, each with its own tests, earlier tests always green.
5. **Double-check** what was asked against what was delivered, point by point.
6. **Independent review** by other agents, turned into work orders (V1–V4) and a final round
   (stronger tests, deliberate rule-breaking, measurement at 2 million documents).

Result: INGEST-4471 fixed, cross-mailbox email dedup implemented, a load file that matches the spec
(with one declared exception, §4), 86 tests, and the sample run gives 16 in, 12 out, 4 suppressed,
identical to a hand-written expected file.

## 3. Method (the loop repeated in every round)

1. **Read and understand** the code, spec and data; state findings and open questions.
2. **Document** the finding or decision before acting on it.
3. **Decide**: I proposed with a recommendation; the engineer decided. Nothing was assumed silently.
4. **Tests first**: each round brings its own tests, which must fail before the fix and pass after
   (policy D-013); all earlier tests stay green as a regression net.
5. **Implement** the smallest change that satisfies the round.
6. **Verify**: build, tests, a run on the sample, a diff against the expected output.
7. **Report** what changed, where, and what was not verified; update log, handoff and file tracker
   at the close of every round.
8. **Review** (the engineer, then independent agents) and correct.

## 4. Phases

| Phase | When | What was done | Outcome |
|---|---|---|---|
| Analysis | 2026-11-06 | Read README, spec, code, sample batch and the buggy output. Translated the README for the engineer to cross-check against his own reading. | Root cause of INGEST-4471 confirmed in the data: a global sha256 `seen` set dropped shared attachments (DOC-000005, 000010, 000011), so DOC-000004 lost 1 of 1 attachments and DOC-000009 lost 2 of 2. Also found: SentDate not in UTC, no §3.3 dedup, latent bugs (nested attachments, orphans, input-order dependence, unquoted CSV, a quadratic diagnostic). |
| Instrument first | 2026-11-06 | Added a minimal stderr logger with no behaviour change; ran the tool on the sample. | The instrumented output was identical to the buggy output, so the reproduction was faithful. |
| Strategy | 2026-11-06 | Test-per-round policy; the open questions Q-1..Q-6 and where they came from; the engineer's table of immutable constraints versus room to decide. | Q-1/Q-2/Q-4 are real spec contradictions or silences; Q-3 follows from §6.1; Q-5 was a weak precaution of mine; Q-6 was process. |
| Domain and scope | 2026-11-08 | The engineer studied e-discovery on his own and wrote working notes; I read them and prepared. Decided: dedup is by family, global dedup (§4 declared not met), nothing beyond the ask is built (enhancements E1–E19 are only documented). | Decisions D-014..D-023. |
| Build R1–R8 | 2026-11-08 | Eight rounds, tests first (table below). | 74 tests, sample as expected, failures never silent. |
| Double check | 2026-11-08 | Point-by-point summary: what was asked, what was delivered, what is extra, what is not verified. | Surfaced the extras (stream read, atomic write, exit codes) and the gaps (2M unmeasured). |
| Independent review | 2026-11-08 | The engineer ran other agents (tests, code against spec, performance at 2M) and gave me their findings as work orders. | One real evidence-loss bug, a null-input crash, weak tests, stale numbers. |
| V1–V4 | 2026-11-08 | V1 evidence loss; V2 input robustness; V4 missing and weak tests, then reorganising the tests by feature. Stopped after each for review. | 74 → 85 tests. |
| Final round | 2026-11-08 | Stronger tests, deliberate rule-breaking (mutation), 2M measurement and documentation, package list. | 86 tests; mutation 8 of 9 detected. |

**The R1–R8 rounds**

| Round | What | Tests after |
|---|---|---|
| R1 | Families first: an attachment is never suppressed alone (the ticket). Sample: 15 out, 1 suppressed, as predicted. | 10 |
| R2 | Dates in exact UTC form | 21 |
| R3 | Valid CSV (RFC 4180, LF, UTF-8 without BOM) | 27 |
| R4 | Nested attachments: FamilyID is the root, ParentID the immediate parent, cycle detection | 35 |
| R5 | Cross-mailbox email dedup (§3.3). Sample: 12 out, 4 suppressed | 52 |
| R6 | Determinism: survivor = lowest DocID, rows sorted; hand-written golden file | 61 |
| R7 | Performance: no quadratic diagnostics; JSON read from a stream | 61 |
| R8 | Failures never silent: clear message, exit codes 0/1/2/3 | 74 |

## 5. How the engineer directed me

Short quotes, translated and abridged. Each one changed what I did.

| Direction, check or pushback | What it did |
|---|---|
| *"Start from zero and record every interaction."* | I created this log and treated the earlier read-only study as outside it. |
| *"We are not going to reinvent anything"* (stay in C#). | No rewrite in another language (D-006, D-022). |
| *"Give me your translation and I will cross it with what I already understood."* | He verified my reading of the README against his own before I proposed anything. |
| *"Checking input and output by hand would be long and pointless; add loggers and debug."* | I instrumented first and proved the bug with a run (phase 2). |
| *"Each round of corrections should have its tests in between, so the tests protect the project."* | Policy D-013: tests per round, earlier tests stay green. |
| *§3.4 against §4 "seems impossible, unless you have an idea."* | I did not force a fit: global dedup, §4 declared not met, with the exact documents `mlee` loses (D-014). |
| He rejected an opt-in `--map` flag and sending questions to the interviewer. | D-017, D-018: decide and document instead. |
| *"Deliver what the exam asks; the rest is an enhancement."* | Scope discipline (D-019): E1–E19 are documented, not built. |
| *"Forget the clock; proceed with the solutions, and ask me if in doubt."* | Closed the time box (D-024); quality over speed. |
| *"Give me a summary, short, so I can double-check what was asked against what you deliver."* | The point-by-point summary that exposed the extras and the unverified points. |
| *"I ran analysis with other agents and we still have gaps."* | Work orders V1–V4 and the final round (section 6). |
| For V2: *keep the current behaviour for the old output file, fix only the comment; yes, catch the out-of-memory error.* | D-035, D-036: his decision, my recommendation. |
| He noticed a hash of only spaces is not treated as empty. | Documented as a known limit, not fixed. |
| *"Update the log, handoff and file tracker at the close of every round, not only at the end."* | I did, from V1 on. |
| *"Let's have a round of conversation before you condense the log."* | He agreed the structure, the voice and the quotes first; this version puts method and direction first and compresses the chronology. |

## 6. Verification, and what went wrong along the way

**Evidence that the work holds**
- **86 tests**, organised by feature (`Dedup`, `Families`, `Invariants`, `Failures`, `Cli`, `Output`,
  `Sample`, `Determinism`). The build has 0 warnings.
- **A golden file written by hand** from the spec and the roadmap, not generated by the tool. The
  sample must match it byte for byte.
- **Independent review** by three read-only agents. It found things I had missed: an email whose
  attachments have no hash could be collapsed and lose one (V1, fixed); a `null` inside `documents`
  ended in a stack trace (V2, fixed).
- **Deliberate rule-breaking (mutation).** I broke eight rules on purpose, one at a time, and checked
  that tests failed. Eight of nine mutations were caught: survivor choice (12 tests fail), attachment
  never suppressed alone (15), empty hash on roots and on descendants (1 and 1), hash case (1),
  Re:/FW: in the subject (1), date in the email key (1), the suppressed counter set to 0 (13).
  The ninth (suppressed = in − out) is equivalent to the real code and cannot be detected by any
  behavioural test. Rules 3–7 are protected by one test each. The production source was restored and
  verified identical to a copy taken before.
- **Scale measured at 2,000,007 documents** (Release, Ryzen 7 5800H, 15.9 GB RAM, synthetic data,
  one run per size): 100,002 docs 2.1 s / 345 MB; 500,009 docs 6.4 s / 1.5 GB; 2,000,007 docs
  34.6 s / 6.0 GB. Time is linear; memory is about 3 KB per document, so free RAM of about 3× the JSON
  size is needed. Complexity O(F + n log n): documents are never compared in pairs because the key is
  a SHA-256.

**What went wrong, said plainly**
- My early table mixed documents and families and confused the engineer; I restated it.
- I relayed another AI's `--map` idea and suggested emailing the interviewer; he rejected both.
- A `.csproj` edit failed because I had not read the file first; nine tests failed for the wrong
  reason. I noticed before drawing conclusions and re-ran.
- R4 tests were green on the first run, because the root resolution was built in R1. The same holds for
  the eight spec-coverage tests added in V4-1. Both departed from "red first" and are declared.
- I wrote "2M ≈ 18 s and ≈ 2.3 GB" as an extrapolation. The real run gave 6.0 GB, about 2.6 times more
  memory. The extrapolation is withdrawn; the measured numbers above replace it.
- I wrote the code, the tests and the early measurements. To offset that, the expected output file was
  written by hand from the spec, and the review and the 2M run were done by other sessions.
- The client's own `DeduperTests` were changed (two passed with a "first one wins" rule). Only
  stricter: any implementation that passed the originals still passes.

## 7. README and spec compliance

| Requirement | Status | How |
|---|---|---|
| README 1, missing attachments (INGEST-4471) | Done | Decide by family; a shared attachment stays in every family. `SharedAttachmentOnDifferentEmails_…` and the sample (DOC-000005, 10, 11 handled). |
| README 2, cross-mailbox email dedup (§3.3) | Done | `EmailIdentity` key. In the sample DOC-000001 and DOC-000009 match; the FW (DOC-000004) and the Re (DOC-000012) do not. |
| README 3, load file matches the spec (§5) | Done | Fixed header and columns, exact UTC date, RFC 4180, LF, UTF-8 without BOM, rows sorted. |
| README 4, tests | Done | 86 tests, each round with its own. |
| README 5, `DECISIONS.md` | The engineer | Written by him by hand; I did not touch it. This log's register is a source. |
| README 6, transcripts | This file | Condensed; the long version is available. |
| §3.1 loose files by sha256 | Done | Case-insensitive; empty hash is "not comparable" and kept. |
| §3.2 vs §3.5 | Resolved by decision | Family first (D-026): an attachment is suppressed only with its family. |
| §3.4 vs §4 | **Not met, declared** | Global dedup; `mlee` loses DOC-000009, 10, 11 and 13 (D-014). |
| §6.1 same output for any input order | Done | Survivor = lowest DocID (ordinal), rows sorted; shuffle and reverse tests. |
| §6.2 about 2 million documents | Measured | 34.6 s, 6.0 GB; memory is the limit. |
| §6.3 summary line | Done | `batch-sample: 16 documents in, 12 out, 4 suppressed as duplicates.` on stdout only. |

**Known limits, accepted and documented:** nested emails are compared by hash only; the email key holds
the set of descendant hashes, not the tree shape; loose files ignore their children; the model has no
CC/BCC; dates without offset are read as UTC and fractions are truncated (one-second tolerance);
`documents` missing is a success with 0 documents; the out-of-memory message is not auto-tested;
real client data was never available.

## 8. Decisions (compact register)

Ids keep their original numbers; housekeeping items are omitted. All decisions below were the
engineer's. Those marked ✓ were my recommendation, confirmed by him, who takes responsibility.

| ID | Decision | Why |
|---|---|---|
| D-006 | Stay in C#/.NET 8. | His native language; no risk to the output contract. |
| D-007 | He writes `DECISIONS.md` by hand. | Explicit instruction. |
| D-008 | Understand everything, then triage. | The exercise holds more than fits in the time box. |
| D-009 | Add logging and run before fixing. | Prove the bug; useful for unseen batches. |
| D-010 | Minimal in-repo logger: stderr, level by `DEDUPTOOL_LOG`. | No new dependency; stdout and CSV stay clean. |
| D-011 | The logging step must not change behaviour. | A "before/after" is only valid if "before" is untouched. |
| D-012 | Runs write outside the project; inputs backed up in `Bkupdata/`. | Keep the original evidence intact. |
| D-013 | Each round ships its own tests; earlier tests stay green. | Regression net. |
| D-014 | Global dedup; §4 not met and documented. | §3.3, §3.4 and task 2 require cross-mailbox dedup. |
| D-015 | Custodian severance not covered. | §4 mentions it only as motivation. |
| D-016 | Same email with different attachments: both families kept. | Nothing is lost; possible double read declared. |
| D-017 | No `--map` flag or second output file (enhancement E1). | Not in the requirement. |
| D-018 | No questions to the interviewer. | His call. |
| D-019 | Scope: what is asked plus the minimum the spec requires. | Spec gaps are documented, not built. |
| D-020 | Every failure is logged and prevented, with one logger. | Never silent. |
| D-021 | Nested emails are compared by hash (E8 for content). | The sample has none; the spec defines no fields for them. |
| D-022 | Stay in C#; O(n) design, measure before optimising. | The limit is memory and JSON reading, not the language. |
| D-023 | Stay close to industry axioms within what is asked. | His criterion. |
| D-024 | Drop the exercise clock; edit code and tests. | Learning the domain took longer; quality over speed. |
| D-025 ✓ | Survivor = lowest DocID (ordinal); email key as in `EmailIdentity`; hashes case-insensitive. | Each was the documented leading option; confirmed one by one. |
| D-026 | Decide by family; roots resolved iteratively with memoisation. | §3.5; per-attachment dedup was the root cause. |
| D-027 | Dates: exact UTC; no offset is read as UTC; bad date gives an empty SentDate and a warning. | The output must not depend on the machine. |
| D-028 | CSV: RFC 4180, LF, UTF-8 without BOM, temp file then rename. | A comma in a file name corrupted a row. |
| D-029 | Rows sorted by DocID, ordinal. | §5 and §6.1 both hold literally. |
| D-030 | Failure policy: bad structure gives a clear error and exit 1; nulls read as empty; empty hash is not comparable. | Never silent; grouping empty hashes would suppress unrelated files. |
| D-031 | Removed the unconditional quadratic diagnostics; invariants are tested instead. | They were the latent O(n²). |
| D-032 | Read the JSON from a stream. | 41–47 % less memory, identical output. |
| D-033 | Emails with no usable date are compared by exact hash only. | Sameness cannot be proven from content; a false negative is the safe error. |
| D-034 | CLI logic moved to `DedupCli.Run`. | Makes the whole flow testable. |
| D-035 ✓ | A failed run leaves a previous output file untouched; the caller checks the exit code. | His decision on V2-2. |
| D-036 ✓ | Catch out-of-memory: clear message, exit 3, not auto-tested. | His decision on V2-3. |
| D-037 ✓ | Failure policy: unsafe output stops; lost secondary information warns and continues; not comparable is kept. `documents` missing and `parentId:""` stay as documented limits. | Confirmed as a rule. |
| D-039 ✓ | The client's `DeduperTests` were made stricter and relocated. | Two passed with a wrong rule. |
| D-040 ✓ | No performance code in the project; results, method and Big-O are text only. | His decision on P5. |
| D-041 ✓ | This log is a condensed, faithful record. | His decision on D9. |
| D-042 ✓ | Package: exclude `bin/`, `obj/`, `.DS_Store`, `Bkupdata/`; include `src/`, `test/` (with `Data/`), `data/`, `README.md`, `SPEC.md`, `DECISIONS.md`, `AI_Log.md`, `.slnx`, `.gitignore`. I delete nothing; he packages. | R9. |

## 9. Where things are

- `src/DedupTool/`: `Deduper.cs` (family-first pipeline), `EmailIdentity.cs` (§3.3 key), `UtcDate.cs`,
  `LoadFileWriter.cs`, `DedupCli.cs`, `BatchValidationException.cs`, `Log.cs`, `Program.cs`, `Models.cs`.
- `test/DedupTool.Tests/`: `Dedup/`, `Families/`, `Invariants/`, `Failures/`, `Cli/`, `Output/`,
  `Sample/`, `Determinism/`, `Support/`, and `Data/` (the sample and the hand-written expected file).
- Never edited by me: `README.md`, `SPEC.md`, `DECISIONS.md`, `data/`, `Bkupdata/`.
