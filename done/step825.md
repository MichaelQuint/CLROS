# Step 825 - M00-001 Durable Gitea Backlog

## Summary

Persisted the reconstructed CLROS backlog as an idempotent, version-controlled
manifest and a preview-first local-Gitea synchronization tool.

## Why

The imported repository had no recoverable tracker state. The manifest now
preserves 16 milestones, 68 labels, 16 epics, and 303 child issues.

## Files changed

- `.gitea/backlog/clros-issues.json`
- `scripts/sync-gitea-backlog.ps1`
- `tests/OS.HostTests/GiteaBacklogManifestTests.cs`
- `tests/OS.HostTests/RepositoryRecoveryTests.cs`
- `docs/issue-labels.md`
- `docs/repo-audit.md`

## Tests run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\sync-gitea-backlog.ps1 -Offline
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\sync-gitea-backlog.ps1 -SelfTest
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

Offline validation and failure-injection self-tests pass. The live tracker was
created with exactly one issue per manifest key and is reverified after every
apply. Final live convergence is verified only after this durable closure
intent is committed and all 23 M00 evidence-review comments are present;
tracker state is administrative evidence and is not part of the Step 846
QEMU/kernel evidence boundary.

## Failures

The first live tracker was empty. Partial-failure, duplicate-key, dependency
cycle, stale-target, manual-closure, and collision-readiness cases are now
covered without storing credentials. Closure updates are ordered
topologically, and recursive dependency/epic checks keep descendants blocked
if a prerequisite update fails partway through an apply.

## Deferred work

Later issue content remains intentionally open and is gated by its dependencies.

## Evidence boundary

This proves recoverable issue intent and additive synchronization. It does not
prove implementation of the later operating-system milestones.
