# Step 320 - Documentation Map and Agent Guide Consolidation

## What Changed

- Shortened the root `AGENTS.md` into a compact operating contract while keeping the project identity, source rules, required reads, build/test commands, evidence contract, Gitea workflow, implementation order, git rules, and final checklist.
- Added `docs/documentation-map.md` to define canonical repository docs, optional Gitea wiki summary pages, update triggers, local override policy, and docs-only verification.
- Updated `README.md` to point agents and readers to the documentation map.
- Updated `docs/completion-gates.md` Gate 14 to record the documentation-map/wiki-split evidence.
- Tightened local PAL override wording so `OSHost_*` is canonical and `SharpOSHost_*` is only a documented compatibility alias.
- Softened `docs/fork-diff.md` so the SharpOS reference cannot override canonical OS repository docs.
- Added `docs/repo-audit.md` and every current `AGENTS.override.md` file to the documentation map.
- Removed the reserved template stub from this step writeup.

## Why

Issue #320 asked to reduce documentation update drag while preserving repository docs as the source of truth for build/run commands, ABI contracts, completion gates, subsystem status, and evidence. The wiki split is now explicit without moving canonical material out of the repository.

## Files Changed

- `AGENTS.md`
- `README.md`
- `docs/documentation-map.md`
- `docs/completion-gates.md`
- `docs/fork-diff.md`
- `work/PAL/AGENTS.override.md`
- `done/step320.md`

## Evidence

Commands run:

```powershell
git status --short
.\scripts\new-done-step.ps1 -Reserve
git diff --check
rg -n "Documentation Map|Gitea wiki|completion-gates|Kestrel support|OSHost_\*" AGENTS.md README.md docs\documentation-map.md docs\completion-gates.md docs\fork-diff.md work\PAL\AGENTS.override.md
rg --files src std apps | Where-Object { $_ -match '\.(c|cpp|h|asm|s)$' }
(Get-Content AGENTS.md | Measure-Object -Line).Lines
rg -n "SharpOS is a reference|SharpOSHost_\*|authoritative reference|canonical for OS|source of truth|source-of-truth" AGENTS.md docs\documentation-map.md docs\fork-diff.md work\PAL\AGENTS.override.md
Invoke-RestMethod -Uri "$api/issues/320/comments" -Method Post -ContentType 'application/json' -Body $payload
```

Live Gitea query:

```text
Issue #320 was open and labeled agent:ready, component:docs, phase:A-baseline, type:docs-adr.
```

Observed evidence:

```text
TEST_START done.step.allocate
NEXT_DONE_STEP done/step320.md
TEST_PASS done.step.allocate
```

- `git diff --check` exited 0. Git reported LF-to-CRLF working-copy warnings for existing text files, but no whitespace errors.
- Documentation spot checks found the new map/wiki split, Gate 14 reference, Kestrel claim boundary, and `OSHost_*` wording in the expected docs.
- Forbidden first-party source-file check over `src`, `std`, and `apps` returned no `.c`, `.cpp`, `.h`, `.asm`, or `.s` files.
- `AGENTS.md` is now 196 lines.
- Gitea read access worked for issue #320, but posting the evidence comment failed with `token is required` because no Gitea credential or token was available in the task environment.

## Failures

- No implementation failures.
- Full solution build was not run because this step touched documentation only and no code, project files, or scripts.
- Could not update or close Gitea issue #320 from this context because the API requires a token for comments/updates and no credential was supplied.

## Deferred Work

- No canonical docs were moved to the Gitea wiki.
- No `done/stepNN.md` history was deleted.
- Remaining subsystem-specific duplication can be reduced in later narrow issues after each owner doc is reviewed.

## Evidence Boundary

This is documentation consolidation only. It does not change code behavior, build output, boot evidence, PAL exports, hosted .NET, Minimal API, Kestrel, cloud-provider support, or completion-gate status beyond documenting the Gate 14 wiki split.
