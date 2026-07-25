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

The corrected live apply ended with:

```text
SYNC_CLOSURE_EVIDENCE requested=23 verified=23
SYNC_VERIFY_PASS milestones=16 labels=68 issues=319
SYNC_SUMMARY created=0 updated=38 unchanged=365 skipped=0 failed=0 wouldCreate=0 wouldUpdate=0
TEST_PASS gitea.backlog.sync
```

The required follow-up preview reported all 403 managed tracker objects
unchanged with `wouldCreate=0`, `wouldUpdate=0`, and `failed=0`. An independent
API census found 23 `agent:done`, one `agent:ready` (`M01-001`), 295
`agent:blocked`, zero `agent:claimed`, zero `agent:review`, 23 unique M00
evidence-review markers, and exactly one `reference:license-blocked`
issue (`M15-013`). The M00 milestone object intentionally remains open with
zero open issues and 23 closed issues.

## Failures

The first live tracker was empty. Partial-failure, duplicate-key, dependency
cycle, stale-target, manual-closure, and collision-readiness cases are now
covered without storing credentials. Closure updates are ordered
topologically, and recursive dependency/epic checks keep descendants blocked
if a prerequisite update fails partway through an apply.

The first M00 closure apply exposed a Gitea 1.26.2 API-boundary mismatch:
`EditIssueOption` silently ignored label IDs sent through the general issue
`PATCH`, even though it accepted body and state changes. Convergence failed
closed with 38 label mismatches. The synchronizer now patches issue fields
without labels, replaces the complete label set through
`PUT /issues/{number}/labels`, validates the returned label count, and
re-reads the issue before continuing. The failed apply created no duplicates;
a rerun is additive and idempotent.

## Deferred work

Later issue content remains intentionally open and is gated by its dependencies.

## Evidence boundary

This proves recoverable issue intent and additive synchronization. It does not
prove implementation of the later operating-system milestones.
