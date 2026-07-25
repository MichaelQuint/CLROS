# Step 832 - M00-008 Agent Guide Identity

## Summary

Corrected CLROS repository, working-directory, solution, and local-Gitea
coordinates in root and directory-local agent guides.

## Why

Agents must not return to the stale repository, solution, or tracker names.

## Files changed

- `AGENTS.md`
- `CLAUDE.md`
- `src/OS/AGENTS.override.md`
- `src/OS.Kernel/AGENTS.override.md`
- `std/no-runtime/shared/AGENTS.override.md`
- `tools/AGENTS.override.md`
- `tests/AGENTS.override.md`

## Tests run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
```

## Evidence

Guides identify `C:\Projects\CLROS`, `CLROS.slnx`, and
`http://localhost:3000/admin/CLROS`; they explicitly retain `OS.*` and
`OSHost_*`.

## Failures

Stale `C:\Projects\OS`, `OS.slnx`, and remote-tracker instructions were removed.

## Deferred work

Subsystem-specific guidance remains in canonical docs.

## Evidence boundary

This is agent-operating-contract evidence only.
