# Step 895 - M02-019 durable M02-002 closure and tracker-convergence transaction

## Summary

Implemented the canonical repository transaction that makes the reviewed and
merged M02-002 result durable in the reconstructed Gitea backlog. The manifest
now contains 16 epics, 328 children, 344 keyed issues, 68 labels, and 428
managed objects. M02 contains 19 children.

M02-002 now records the accepted issue, pull request, source, review, merge, and
host-evidence anchors and requests the closed/`agent:done` state. M02-019 is the
new dependency-ordered closure transaction. The only existing dependency
rewrite is:

```text
M02-017: M02-002 -> M02-019
```

The resulting chain is:

```text
M02-018 -> M02-002 -> M02-019 -> M02-017 -> M02-003
```

The synchronizer also rejects multiple hidden issue-key markers in one body,
adopts one uniquely pre-keyed remote issue without proposing a duplicate, and
continues to fail closed when reviewed closure evidence is absent.

## Why

M02-002 issue `#42` was independently reviewed and merged through PR `#431`,
but the pre-transaction manifest still requested an open state. That left the
tracker at 280 open issues before the M02-019 bootstrap and kept M02-017
blocked despite accepted production C# existing on `master`.

M02-019 persists that accepted result and provides a guarded, idempotent path
to close M02-002 and M02-019 in dependency order. It intentionally owns only
the catalog, synchronizer, their host tests, and repository-state
documentation. It does not alter EFI production source; after convergence,
M02-017 is the next serialized production C#/real-QEMU issue.

## Files changed

Implementation commit `8e29ee9de9d2ea8cb2db36a1184399f2c65b4d31`
with tree `2af05d3916e0913d7ba2810e4b45f0249a3a07f7` is based directly on
accepted M02-002 merge commit
`da856db657575d8f8d67713c7afe96e113854e54`.

The implementation commit changes exactly six authorized paths, with 519
insertions and 125 deletions:

- `.gitea/backlog/clros-issues.json`
- `scripts/sync-gitea-backlog.ps1`
- `tests/OS.HostTests/GiteaBacklogManifestTests.cs`
- `tests/OS.HostTests/RepositoryRecoveryTests.cs`
- `docs/issue-labels.md`
- `docs/repo-audit.md`

This evidence commit adds only this newly and uniquely reserved ledger:

- `done/step895.md`

No `src/**`, `std/**`, application, production C#, EFI entry/project,
`BootSequence.cs`, `Probes.cs`, QEMU runner, completion-gate, reference, or
existing done-step path changed in the implementation commit.

## Tests run

Focused checks passed before the authoritative gate:

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\sync-gitea-backlog.ps1 -Offline
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\sync-gitea-backlog.ps1 -SelfTest
git diff --check
```

The frozen-source authoritative runner used evidence root:

```text
C:\Projects\CLROS-M02-019\artifacts\logs\m02-019-final\8e29ee9d-20260730T230658Z-r1
```

It ran the following ten commands from implementation commit `8e29ee9`:

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-efi-shell.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\sync-gitea-backlog.ps1 -Offline
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\sync-gitea-backlog.ps1 -SelfTest
git diff --check
git status --short
```

All ten commands exited `0`. Their durations and stdout SHA-256 values were:

| Step | Seconds | stdout SHA-256 |
|---|---:|---|
| solution build | 20.573 | `0853499686121876E476F1EEB5A85EA8E52938E77D17F915E738DC6ACBFFB8ED` |
| HostTests build | 4.051 | `88AA1ACD9FD662FCC45E433BC13F346939BFF468F4E7AF8EE87AAC91F374C484` |
| complete HostTests | 2438.740 | `357E5177A438374D424EAE9F763851868530889550431C383BB32A11D7F4A429` |
| naming/source firewall | 28.260 | `F859FFF4688D92C662BD161F4DEDF4C64778B133B7ABDBED70D634D8A2F4B651` |
| QEMU dry-run | 85.557 | `1D89BEBC2418F4BF887D21B84366BBE7AF2E555908570CCF1B87B415F5C91C95` |
| EFI build/preflight | 43.335 | `5572D44126239FD1566AA8DD5FF63E100195AE1FA2D67EB8961A408467FB8D9E` |
| synchronizer offline | 4.054 | `6E359AA6A75CAA4397F4E5970E9CBDEA78935F6C648539D8179430EA35A114EC` |
| synchronizer self-test | 5.041 | `55BBC6CFE7B06B765DEDFBD4697BD9F727D233AE236113D6691888E6B0346EFA` |
| `git diff --check` | 1.016 | `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855` |
| clean `git status --short` | 1.007 | `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855` |

Every captured stderr stream was empty with SHA-256
`E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.
Both builds reported zero warnings and zero errors.

## Evidence

The complete HostTests run contained:

```text
TEST_START count: 3339
TEST_PASS count: 3340
TEST_FAIL count: 0
terminal TEST_PASS host.tests count: 1
terminal line: TEST_PASS host.tests
```

The exact M02-019 envelope occurred once, adjacent and ordered at lines
1721-1723:

```text
TEST_START m02.019
CLROS_M02_019_READY epics=16 children=328 issues=344 managed=428 m02Children=19 m02002Issue=42 m02002Pr=431 m02002Merge=da856db657575d8f8d67713c7afe96e113854e54 closureMarkers=2
TEST_PASS m02.019
```

Offline synchronization proved exact idempotent inventory:

```text
SYNC_MANIFEST_VALID milestones=16 labels=68 epics=16 children=328 issues=344
SYNC_MODE preview
SYNC_SUMMARY created=0 updated=0 unchanged=0 skipped=428 failed=0 wouldCreate=0 wouldUpdate=0
```

The synchronizer self-test proved both collision-safe adoption and duplicate
marker rejection:

```text
SYNC_SELF_TEST_MANAGED_BODY_PASS
SYNC_SELF_TEST_HIDDEN_KEY_ADOPTION_PASS key=M02-019 markers=1 wouldCreate=0 duplicateMarkersRejected=true
SYNC_SELF_TEST_STATE_POLICY_PASS
TEST_PASS gitea.backlog.sync
```

A fresh authenticated, no-write live preview was also captured before review.
It exited `1` by design because M02-019's reviewed-evidence marker was not yet
posted:

```text
SYNC_REMOTE_READ labels=68 milestones=16 keyedIssues=344
SYNC_CLOSURE_EVIDENCE requested=65 verified=64 marker=clros-evidence-reviewed
SYNC_ITEM_FAIL issue=M02-019 operation=preview reason=Closure for 'M02-019' requires an evidence-review comment containing '<!-- clros-evidence-reviewed:M02-019 -->'.
SYNC_KEYS action=wouldCreate count=0 keys=none
SYNC_KEYS action=wouldUpdate count=5 chunk=1/1 keys=issue:M02-002,issue:M02-017,issue:M02-018,issue:M02-EPIC,issue:M04-001
SYNC_SUMMARY created=0 updated=0 unchanged=422 skipped=0 failed=1 wouldCreate=0 wouldUpdate=5
```

This proves the pre-keyed live issue `#432` is adopted without duplicate
creation and that the closure policy fails closed before independent review.
The preview stdout SHA-256 is
`202EC9D23D886A8D9C060354D3D35BF7348094639F473734FB972A9974BE7555`.

The canonical EFI regression/preflight produced `BOOTX64.EFI` with:

```text
bytes: 55808
SHA-256: 158432A6F1BA692561A8CA047AD4E1C1362E3549E5CA8A7EF4E7288ABECD1741
```

The evidence summary SHA-256 is:

```text
226468BBD384181E46E1376264E754FE201E74B5A0DD4771C3E89EB305E85C15
```

The independently rehashed 36-entry evidence ledger reported zero missing
files, zero byte mismatches, zero SHA-256 mismatches, and has SHA-256:

```text
C0D19367BBA4F0B7B4EE0A881B16625F8D2496B72188AA2764080708EF79F72E
```

Two independent frozen-head source reviews found no actionable ownership,
manifest, synchronizer, test, documentation, or PowerShell-compatibility
defect.

## Failures

- An early focused-check wrapper incorrectly treated captured PowerShell
  stdout arrays as native process exit codes. Each command was rerun using
  native `$LASTEXITCODE` handling and passed. The faulty aggregate result was
  not used as evidence.
- After HostTests exited `0`, the authoritative runner's
  `Start-Process -Wait` retained two reusable .NET compiler/MSBuild server
  descendants. `dotnet build-server shutdown` stopped those servers
  gracefully, released the same runner, and allowed it to record HostTests
  exit `0` without rerunning or changing source. The same condition was
  cleared once after QEMU dry-run. This was wrapper process-tree behavior, not
  a test hang or failure.
- The authenticated pre-review preview exited `1` intentionally because the
  M02-019 reviewed-evidence marker was absent. It made no writes and proposed
  no issue creation.
- Two draft evidence-summary serialization attempts were discarded before
  sealing the ledger: one used invalid PowerShell boolean literals and one
  serialized an extended file-line object. The final compact summary was
  overwritten, parsed back successfully, and independently covered by the
  zero-mismatch 36-entry ledger.

No acceptance test, build, naming check, EFI preflight, offline convergence
check, self-test, or source-integrity check failed.

## Deferred work

- Push the evidence commit, open the M02-019 pull request, and obtain an
  independent review of the exact pushed head and 36-entry evidence ledger.
- Post the exact M02-019 reviewed-evidence marker only after that review.
- Merge the reviewed head and run guarded preview/apply/repeated-apply/final
  preview from canonical `master`.
- Verify M02-002 issue `#42` and M02-019 issue `#432` are closed with exactly
  `agent:done`, no duplicate hidden keys exist, and M02-017 issue `#427`
  becomes the smallest collision-safe `agent:ready` issue.
- Claim M02-017 and implement its owned EFI production C# integration, exact
  offset-zero cookie execution, cumulative M01/M02 marker policy, and fresh
  real QEMU/OVMF evidence.

## Evidence boundary

M02-019 is a repository-state transaction backed by host tests. Its QEMU
operation was dry-run only, and its EFI operation was a regression/preflight
build only. This step does not prove guest execution, the M02 page linked into
the EFI entry path, live In8/Out8 execution, COM1 ownership, real
`ExitBootServices`, provider support, hosted .NET, Kestrel, CloudReady status,
or production readiness.
