# Step 414 - Claude agent guide consolidation

## Summary

Replaced the stale untracked `CLAUDE.md` with a concise OS-specific Claude
agent guide that points back to canonical repository docs and removes credential
text.

## Why

The old guide duplicated broad subsystem detail, used stale project framing,
and included credentials. Agents need a short handoff that preserves the OS
identity, evidence boundaries, Gitea workflow, and Kestrel claim limits without
becoming a second source of truth.

## Files changed

- `CLAUDE.md`
- `done/step414.md`

## Tests run

- `rg -n "ilove|Password:|Username:|SharpOS is the primary|new OS" .\CLAUDE.md`
- `rg -n 'Project name is `OS`|docs/completion-gates.md|docs/documentation-map.md|OSHost_\*|Kestrel' .\CLAUDE.md`
- `.\scripts\new-done-step.ps1 -Reserve`
- `git diff --check`

## Evidence

- Credential/stale-framing check returned no matches.
- Guardrail check found the expected completion-gate, documentation-map,
  `OSHost_*`, and Kestrel claim-boundary references in `CLAUDE.md`.
- Done-step reservation emitted:
  - `TEST_START done.step.allocate`
  - `NEXT_DONE_STEP done/step414.md`
  - `TEST_PASS done.step.allocate`
- `git diff --check` passed.

## Failures

None.

## Deferred work

- Continue creating small `agent:ready` child issues under the currently blocked
  Gitea epics.
- Keep future Claude guidance changes aligned with `AGENTS.md` and
  `docs/documentation-map.md`.

## Evidence boundary

This is documentation hygiene only. It does not prove hosted .NET, ASP.NET
Minimal API, Kestrel, cloud readiness, production readiness, or any new runtime,
driver, storage, networking, PAL, or boot behavior.
