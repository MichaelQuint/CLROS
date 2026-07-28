# Step 878 - Make the M01 canonical paging fixture deterministic

## Summary

Repaired the reopened M01-030 host-fixture regression without changing the
production paging planner. The canonical M01 live-paging fixture still owns and
clears 128 pages (512 KiB), but its native allocation now begins on an exact
2 MiB page-table coverage boundary. An explicit host test pins:

- 128 pages / 524,288 logical bytes.
- A logical length no larger than one 2 MiB PT-coverage interval.
- 2,097,152-byte base alignment.
- Four table pages for both the legacy and exact-live mapping profiles.

This removes the fixture's dependence on ASLR, module-initializer order, GC, and
native allocator placement while avoiding the fourfold allocation and clearing
cost of a 2 MiB logical fixture.

The durable backlog contract was corrected before implementation closure. The
temporary reopen state also exposed two historical blocker-ledger phrases that
had been dropped from managed current-evidence text; those terms were restored
without changing implementation claims or evidence boundaries. M01-030,
M01-031, and M01-032 are reclosed only after a source-bound full-harness pass.

## Why

M01-025 added an earlier module initializer and thereby perturbed host native
allocation placement enough to expose a pre-existing M01-019/M01-030 fixture
assumption. When a merely 4 KiB-aligned 512 KiB backing started in the final six
pages of a 2 MiB interval, the legacy profile could span two PT keys while the
exact-live profile remained in one. The production planner correctly reported
five versus four table pages, but the fixture incorrectly required equal
geometry.

Allocator padding, forced collection, module reordering, or weakening the
planner would only hide the issue. Aligning the existing logical fixture to the
hardware coverage interval makes the invariant structural and deterministic.

## Files changed

- `tests/OS.HostTests/M01LivePagingIntegrationTests.cs`
  - Aligns the canonical 512 KiB backing to 2 MiB.
  - Adds independent backing-size, interval, alignment, and 4/4 geometry
    assertions.
- `.gitea/backlog/clros-issues.json`
  - Corrects the optimized M01-030 regression contract.
  - Preserves M01-028 blocker-ledger terms.
  - Records final M01-030 evidence and M01-031/M01-032 revalidation.
  - Returns M01-030, M01-031, and M01-032 to `agent:done` / closed.
- `done/step878.md`
  - Preserves this regression diagnosis, implementation, validation, and
    evidence boundary.

No production, EFI, kernel, application, completion-gate, or reference source
changed.

## Tests run

- `dotnet build .\CLROS.slnx --nologo -m:1`
  - Passed with zero warnings and zero errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Passed with zero warnings and zero errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - Passed from implementation commit
    `9e21bff98cf3fd9c2c304cc2aedbf2c90445c66a`.
  - Final non-empty line was exactly `TEST_PASS host.tests`.
  - `TEST_FAIL` prefix count was zero.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\check-naming.ps1`
  - Emitted `TEST_PASS naming.guard`.
  - Emitted `TEST_PASS source.firewall`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\run-qemu-smoke.ps1 -DryRun`
  - Emitted `TEST_PASS qemu.runner.dryrun`.
  - This was a tooling/configuration check, not boot evidence.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\sync-gitea-backlog.ps1 -SelfTest`
  - Emitted `SYNC_SELF_TEST_MANAGED_BODY_PASS`.
  - Emitted `SYNC_SELF_TEST_STATE_POLICY_PASS`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\sync-gitea-backlog.ps1 -Offline`
  - Validated 16 milestones, 68 labels, 16 epics, 321 children, and 337
    issues with zero failures.
- `git diff --check`
  - Passed.

## Evidence

Implementation:

- Branch: `codex/M01-030-fixture-determinism`
- Commit: `9e21bff98cf3fd9c2c304cc2aedbf2c90445c66a`
- Pull request: `#404`
- Merge commit: `a4c0a08b8299c3c84db83a54b8f1df3ea8822cf5`
- Changed implementation path:
  `tests/OS.HostTests/M01LivePagingIntegrationTests.cs`

Full host evidence:

- Local stdout:
  `artifacts/m01030-regression-evidence-20260728-041724-136/host.out.log`
- Stdout size: 829,234 bytes
- Stdout SHA-256:
  `728E203C256B21E94FD191B9D15E7AD281DE4EAB2BDA93F5050B1075303E8306`
- Local stderr:
  `artifacts/m01030-regression-evidence-20260728-041724-136/host.err.log`
- Stderr size: 0 bytes
- Stderr SHA-256:
  `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`
- Each of these exact lines appeared once:
  - `TEST_PASS m01.019 canonical fixture uses deterministic PT-coverage backing`
  - `TEST_START m01.030`
  - `CLROS_M01_030_READY`
  - `TEST_PASS m01.030`
  - `TEST_START m01.031`
  - `CLROS_M01_031_READY`
  - `TEST_PASS m01.031`
  - `TEST_START m01.032`
  - `CLROS_M01_032_READY`
  - `TEST_PASS m01.032`
  - `TEST_PASS host.tests`

Durable contract repairs:

- PR `#402`, merge
  `d2056e30aaf9b5f21c4f5cd48c6512ecc3ab9aea`, corrected the M01-030
  512 KiB logical / 2 MiB alignment contract.
- PR `#403`, merge
  `4e0b4b446076f320464c66efa97b02c77a9e1ea6`, restored M01-032's
  historical blocker-ledger terms.
- Issue `#385` comment `#6122` records the additive scope correction.
- Issue `#385` comment `#6132`, issue `#386` comment `#6134`, and issue
  `#387` comment `#6135` carry fresh evidence-review/revalidation markers.
- Independent implementation, portability, performance, manifest-contract,
  and closure reviews found no remaining blocker.

## Failures

- The first complete regression run reproduced the address-sensitive fixture
  mismatch as `legacy=5 live=4`; this established the exact defect.
- An initial proposed 2 MiB logical allocation was rejected in review because
  it would quadruple fixture allocation and clearing. The final repair keeps
  512 KiB and uses 2 MiB alignment only.
- Two pre-final full-harness attempts stopped at
  `TEST_FAIL m01.028 blocker_ledger`.
  - The first revealed that the M01-030 reopening text had dropped historical
    `AcpiScratch`, `FramebufferMetadata`, `SerialStub`, and return-target
    terms.
  - The second revealed that the temporary M01-032 reopening text had dropped
    `ordinary C#`, return-address, `popfq`, and `RFLAGS` terms.
  - Both were repaired additively in the manifest and managed issue bodies;
    the final full harness passed.
- No generated artifact or failed-run log is committed.

## Deferred work

- This is host-only deterministic-fixture evidence. It does not invoke EFI,
  allocate firmware pages, install page tables, execute a privileged
  transition, or observe CR3, PTE, TLB, or RFLAGS effects in hardware.
- The QEMU dry run is not real QEMU/OVMF proof.
- No completion-gate status changes.
- M01-025 remains the active serialized EFI integration issue. Its final
  source-bound EFI build and complete host validation must include this repair
  before review and closure.
