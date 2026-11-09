# Decisions

Keep this short and honest. Bullet points are fine. We read this before we read your code.

* I did this test on Friday after my job, and I worked through the weekend. So all questions I had I just wrote them down since my team was not available to answer (is weekend).

* I spent about 1 hour on implementation and refining, HOWEVER; I spent like 4 hours or so learning about demail e-discovery (families, custodians,
dedupikcation, practice, files loading) with this I wrote down the rules and risks of the decisions that could be taken along development round.

- I decided to focus on "family matters", before the solution was discarding any repeated hash individually. Now the decision is taken focusing on "family matters" rule, either the whole family stays or not.

- Since there was no information about "what makes two emails -the same-" criteria I took this decision: 
** I used: sender, recipients, subject, sent time as a UTC instant, normalised body, and attachment's hashes. 
** A false positive loses evidence and a false negative only costs reading time, so I made it conservative.

- Per rule 3.2: I decided families come first. An attachment is dropped only when its whole family is dropped. 
- Suppressing attachments one by one was the cause of the bug.

## Where the spec was unclear, contradictory, or silent

NOTE: Since I took this test on friday afternoon I assumed that team was not available for answering questions so I just wrote my questions down and kept on considering possible sceanrios instead:

For each: what the question was, what you decided, what else you considered, and why you chose as you did. If you asked us instead of deciding, say so.

My questions:
 ** Why can't we add an extra column to CSV? 
 what kind of dataBase are we using? SQL or NoSQL? PostgreSQL allows using both because of its native support for JSON and JSONB files. 

- Rules 3.4 and 4 were contradictory:
    * A CSV with a header row. **Column order and header text are fixed. The ingest job breaks if they change.**
    * 3.4 Deduplication is performed across the entire batch.
    * 4: Each custodian's production set must be complete and self-contained, so that it can be produced on its own if that custodian is later severed from the matter.

- Rule 3.2 (identical attachments are duplicates) vs 3.5 (families stay intact):

- Rule 3.3 does not say what makes two emails "the same". 

- Information didn't say anything about "nested emails" (section 2 mentions them but not deduplication criteria).

- When a custodian leaves the process, this was not mentioned, but I foresaw it as a possible scenario since it is common in the industry.
**  With global dedup, if the holder of the surviving copy leaves, the others lose that document. See "What I'd do next".

- Missing sha256: section 2 says every document has one, but not what to do if it is empty (unreadable file, failed hash step, dropped field). 

- Bad input (a cycle in parentId, a missing parent, a repeated docId): not specified. The run stops with a clear message and exit code 1. An unreadable date only warns.

- 6.2 says "roughly two million" documents but gives no time or memory budget. I measured 2,000,007 synthetic documents: 34.6 s and 6.0 GB. Time grows linearly; memory is the real limit.

- Which copy survives: not specified. I keep the lowest DocId, so the result does not depend on the input order (6.1).

- I was thinking about modifying the CSV to add a new column called "AllCustodians" in which I'd add the name of those who also share the file.

## What I changed and why

- I had to change the solution was working. Before my fixes the solution was breaking one of the most improtant rules on e-discovery e-mail industry "Faimly matters most!" and that's why at the end we had emails with no attachements. This was one of the most important changes.

- I added a logger since this process didn't have one. Not having good logging practices leads to "guess" where the error could be and this can lead to over-work, is better to have a well structured chain of events in order to apply iprovements

- at: EmailIdentity.Key - I decided to compare normalized content instead of the hash, because the same email hash changes in different mailboxes

- In Deduper.ResolveRoots: 
** before, it looked only one level up (byId[d.ParentId]), so an attachment of a nested email got its direct parent as FamilyID.

** Now it walks up to the real root, and a cycle or a missing parent raises a BatchValidationException.

- I corrected the way the date was converted in order to meet point §5 format.

- I corrected the CSV so a name with a comma wont break the row; the order stays put

- In DedupCli I split the run logic out of Program.Main and added exit codes: 
** 0 ok, 1 invalid batch, 2 usage, 3 unexpected (This also includes "out of memory").
** Before, bad input ended in an unhandled exception.

- I updated the way that we used to discard files. Before it was "first file that arrives wins", which caused a bug when processing the batch in a different order. Now the process relies on the field "DocId" 

- Performance: the dedup itself was already linear. 
** I removed an O(n²) integrity scan. 
** IO read the JSON from a stream instead of ReadAllText. 
** Memory dropped between 41 and 47%, measured, with the same output.

- I decided to keep documents that have no hash and log a warning:
** there is no way to prove they are duplicates.

- I addedd new tests, organised by feature, with a hand-written expected CSV for the sample. 
** I strengthened two of the provided tests because they also passed with a wrong "first one wins" rule.

## What I didn't get to

Known gaps, things you'd flag in a PR description, tests you know are missing.

- point §4 not met. The spec required each custodian’s set to be complete on its own. I choose to apply "global deduplication", which causes that only one instance appears in the CSV in case of duplicated families. 
** In order to satisfy §4, a second file—detailing "who else had it" would have been necessary; 
** ** I did not include this, as it was not requested. But it can be added

- §6.2 (2M) is verified only using synthetic data, on my machine, with one run per size. 
** There is no automated test for the out-of-memory warning.

- §3.3 is prepared in a conservative manner. 
** Edge cases are addressed in the transition notes.

## What I'd do next

- Gather all needed information in order to ask other "edge cases" or non described scenarios so I could also analyze them and forsee more possible edge cases

- Create a solid documentation for AI about what is the "industry standards" so it considerd them when suggesting plans or giving ideas.

- Ask the client if we can add a column or a second file with "who else had this document". 
** It would meet §4 and cover a custodian leaving, without changing the global dedup. 
** A second option is a setting to choose global or per-custodian dedup, with global as default.

- Duplicates map / "all custodians" list. Addresses §4.
** This point requires agreement with the client on whether an additional column or file can be included, as the contract specifies a fixed set of columns.

- Implement incremental JSON parsing and discard content bodies after hashing. 
** Currently, the entire batch resides in memory (approximately 3 KB per document). Discarding body after calculating its hash would reduce this figure to an estimated 0.5 KB (this is an estimate)

- Agree with the client on one failure policy (today a missing "documents" array stops the run, but an unreadable date only warns).

- Parallelize by family. 
** This leverages multiple CPU cores while remaining deterministic, as the "surviving" document is selected based on its DocId rather than the order in which it is processed.

- Load testing using a synthetic generator. 
** The current measurement for a 2-million-document dataset was performed using a generator that is not part of the project.

## If you reimplemented in another language

Why, and what you did to make sure the output contract is preserved.

- N/A. I stayed in C#/.NET 8: the work is linear and the bottleneck I measured was memory and JSON reading, not the language.

## Working with the AI

What it got right, what you had to correct or reject, and anything you'd warn a teammate about.

What got Right
- AI had no issue on spotting logic and contradictions behind the given excecrise 
- AI gave good advices about how could we solve this issue and explaining that it was not possible for it to give a solid answer since there was contradictory logic.
- AI excels at giving me all context behind the E-discovery, and more precisely behind e-mail e-discovery processes, rules, recommendations, software solutions and companies that succes on this matter
- AI gave very good reviews to found defects and potential bugs

What I had to correct or reject:
- AI gave some suggestions that leaded to silent failing
- AI gave ideas that solved the issue straight forward but didn't obey best coding conventions. Also some ideas left open perforamnce gap
- AI did a fix for 2M as extrapolation; I asked it to reun the real thing
- Created tests that were green from the begining, I asked for mutation just to prove them right
- 

What I'd warn a teammate about:
- If an AI agent writes code, then use another Agent in another session to do the testing and measuraments, ask for independent review.
- Remember AI needs right instructions, having a proper skill or tool to address each situation is better, this goes in hand with my previous recommendation, use a review agent with review tools.
- Check every fact AI states, for example, it extrapolated 2M document before actually measuring
- AI loves inventing facts or assuming, so you better question it about where it got it's resources or information and discourage from providing information with no foundation or solid arguments