# Step 334 - AGENTS and PAL Override Consolidation

## Summary
Finished a small documentation-consolidation slice for Gitea issue #320 by keeping the root agent guide compact and trimming the PAL directory override to local-only rules.

## Why
Issue #320 requires repo docs to remain canonical while reducing duplicate agent guidance. `AGENTS.md` already carried the project-wide invariants; `work/PAL/AGENTS.override.md` was repeating broad PAL ABI policy that belongs in `AGENTS.md`, `docs/pal.md`, and `work/PAL/pal-design.md`.

## Files changed
- `AGENTS.md`
- `work/PAL/AGENTS.override.md`
- `done/step334.md`

## Tests run
- `git diff --check`
- Read-only subagent audit of `AGENTS.md`, `README.md`, `docs/documentation-map.md`, and `work/**/AGENTS.override.md`

## Evidence
- `AGENTS.md` now has a single `## Final Checklist` and keeps the Gitea credential caveat, forbidden-source/secret checks, and uncommitted-file reporting requirement.
- `work/PAL/AGENTS.override.md` is now a short local override that points detailed ABI rules back to the canonical PAL docs.
- The read-only auditor confirmed `AGENTS.md` remains materially shorter while preserving core invariants, `docs/documentation-map.md` identifies canonical repo docs and optional wiki summaries, and the PAL override is now directory-local in scope.
- `git diff --check` exited 0; output contained only existing LF-to-CRLF warnings.

## Failures
- The first checklist edit briefly duplicated `## Final Checklist`; the auditor caught it and the duplicate block was removed before evidence was recorded.
- Gitea issue updates remain blocked in this environment because no API credentials are available.

## Deferred work
- Continue trimming duplicate hosted .NET/Kestrel/PAL claim-boundary prose in subsystem docs without deleting local warnings that prevent false readiness claims.
- Do not move canonical build, ABI, gate, or evidence material to the wiki.

