# Step 847 - M00-023 Backlog Evidence-Contract Repair

## Summary

The durable CLROS backlog and live local Gitea tracker were repaired so the
first 15 M01 implementation slices close only from honest host evidence and feed
a serialized 11-issue guest-integration chain. The guarded synchronization
created only `M01-016` through `M01-026`, updated the exact derived set of 24
existing issues, verified all 331 keyed issues, and converged to 415 unchanged
managed objects.

## Why

The original M01 contracts required each leaf to produce real-QEMU markers even
though those issues did not own `BootSequence.cs`, `OS.csproj`, `EfiEntry.cs`,
the QEMU runner, or another live guest integration surface. That made otherwise
small implementation issues impossible to close honestly and encouraged
metadata-only evidence to masquerade as guest proof.

## Files changed

- `.gitea/backlog/clros-issues.json`
- `scripts/sync-gitea-backlog.ps1`
- `tests/OS.HostTests/GiteaBacklogManifestTests.cs`
- `tests/OS.HostTests/RepositoryRecoveryTests.cs`
- `docs/issue-labels.md`
- `docs/repo-audit.md`
- `done/step847.md`

## Tests run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\sync-gitea-backlog.ps1 -Offline
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\sync-gitea-backlog.ps1 -SelfTest
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\sync-gitea-backlog.ps1
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\sync-gitea-backlog.ps1 -Apply
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\sync-gitea-backlog.ps1
git diff --check
git status --short
```

## Evidence

Implementation commit `66d789c` was pushed on
`codex/M00-023-evidence-contract-repair` before the live tracker mutation.

The manifest and offline validator report:

```text
SYNC_MANIFEST_VALID milestones=16 labels=68 epics=16 children=315 issues=331
TEST_PASS gitea.backlog.sync
SYNC_SELF_TEST_MANAGED_BODY_PASS
SYNC_SELF_TEST_STATE_POLICY_PASS
```

The complete host harness ended with `TEST_PASS host.tests`, contained no
`TEST_FAIL`, `OS_SMOKE_FAIL`, or `SYNC_FAIL` marker, and wrote zero stderr
bytes. Its final stdout was 364,029 bytes with SHA-256
`B0FEC545C5EEB9DDE2EB502509AB2E72DAF609A8478F0EF3A627AA83C7C84CB8`.
The empty stderr SHA-256 was
`E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.

The reviewed pre-apply preview reported:

```text
SYNC_REMOTE_READ labels=68 milestones=16 keyedIssues=320
SYNC_KEYS action=wouldCreate count=11
SYNC_KEYS action=wouldUpdate count=24
SYNC_SUMMARY created=0 updated=0 unchanged=380 skipped=0 failed=0 wouldCreate=11 wouldUpdate=24
TEST_PASS gitea.backlog.sync
```

The 11 create keys were exactly `M01-016` through `M01-026`. The 24 update
keys were `M00-023`, `M00-EPIC`, `M01-001` through `M01-015`, `M01-EPIC`,
`M02-001`, `M02-EPIC`, `M03-007`, `M03-EPIC`, `M04-001`, and `M04-EPIC`.
`M03-EPIC` is an intentional derived update because its generated child
checklist embeds the changed `M03-007` title.

The guarded apply reported:

```text
SYNC_VERIFY_PASS milestones=16 labels=68 issues=331
SYNC_SUMMARY created=11 updated=24 unchanged=391 skipped=0 failed=0 wouldCreate=0 wouldUpdate=0
TEST_PASS gitea.backlog.sync
```

Gitea assigned issues #323 through #333 to `M01-016` through `M01-026`.
M00-023 remained claimed as issue #321 and M01-001 remained claimed as issue
#25 during the apply. The final no-write preview reported:

```text
SYNC_REMOTE_READ labels=68 milestones=16 keyedIssues=331
SYNC_SUMMARY created=0 updated=0 unchanged=415 skipped=0 failed=0 wouldCreate=0 wouldUpdate=0
TEST_PASS gitea.backlog.sync
```

Contract comment `<!-- clros-contract-amendment:M01-001-host-evidence -->` was
posted to issue #25 as comment 5337. It records that
`CLROS_M01_001_READY` is a host-harness marker and that live integration belongs
to M01-016 through M01-026.

## Failures

One delegated full-harness attempt timed out after 304 seconds without captured
output; it was not cited as passing evidence. Root reran the complete harness in
the background and obtained the passing result above.

The first live preview exposed 24 updates rather than the initially modeled 23.
No write had occurred. Inspection showed the extra `M03-EPIC` update was the
correct generated consequence of changing its `M03-007` child title. The
manifest, tests, and documentation were corrected to 24, revalidated, and
previewed again before apply.

The first Git push was rejected by a stale credential-manager entry. No secret
was printed or written to the repository. The branch was pushed successfully
using the securely configured local Gitea identity.

## Deferred work

M01-001 is implemented but requires its separate reviewed closure evidence.
M01-002 through M01-015 remain host-only implementation slices, while
M01-016 through M01-026 remain the serialized live-integration and real-QEMU
proof path.

The SharpOS permission audit found no incorrect current
`reference:license-blocked` issue: only the isolated GPL/WAD DOOM fixture
retains that label. Four generic provenance sentences should later be qualified
to mention the documented SharpOS exact-file-hash/owner-grant exception without
weakening third-party, GPL, CoreCLR, vendor, or media boundaries.

## Evidence boundary

This step proves only that the backlog, collision ownership, dependency graph,
evidence classification, and live Gitea representation are coherent,
recoverable, additive, and converged. It does not implement M01 memory or
paging behavior and does not prove a live guest, real `ExitBootServices`, owned
COM1, devices, application execution, hosted .NET, Kestrel, cloud support,
CloudReady status, or production readiness.
