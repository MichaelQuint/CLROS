# Step 873 - M01 live-paging prerequisite recovery

## Summary

Completed M01-028's durable backlog-recovery boundary. A read-only feasibility
audit established that M01-025 could not honestly wire the reversible CR3 path
against the current contracts. The version-controlled manifest now inserts
four serialized host-only prerequisites:

1. M01-029 separates deterministic preparation from caller-supplied synthetic
   execution transcripts.
2. M01-030 replaces historical identity aliases with exact live-interval
   bindings.
3. M01-031 sizes, materializes, and reads back the physical page-table arena.
4. M01-032 supplies an audited composite call gate with an exact return site
   and immediate post-return RFLAGS observation.

M01-025 remains blocked on M01-024, M01-027, and M01-029 through M01-032.
M01-026 remains the owner of cumulative real-QEMU/OVMF paging proof.

The canonical catalog now contains 16 epics, 321 children, 337 keyed issues,
and 421 managed tracker objects. The synchronizer, dedicated manifest tests,
shared M00 repository-recovery invariant, and administrative documentation all
use those exact current figures while preserving older counts as historical
evidence.

## Why

The existing host model has five evidence defects that cannot be repaired
honestly in `EfiEntry.cs` alone:

- its flat page-table entry workspace is deliberately disjoint from the
  physical table arena that would become CR3;
- its required `SerialStub`, ACPI-scratch, and framebuffer-metadata identity
  records are historical aliases or synthetic provenance, not exact
  composite-interval dependencies;
- its exact table-page count depends on a physical candidate address that must
  be selected before the firmware allocation call;
- an ordinary NativeAOT C# unmanaged call cannot publish the exact call-next
  RIP required by the composite, and the composite's final `popfq; ret` cannot
  observe hardware RFLAGS after restoration; and
- a modeled success path still accepts caller-supplied CR3, protection,
  invalidation, and probe transcripts, which cannot be promoted to guest
  execution evidence.

Attempting M01-025 without these contracts would either fabricate evidence,
load unmaterialized table bytes, weaken exact-return validation, or emit
post-transition claims without a hardware observation. M01-028 therefore
repairs the dependency graph instead of making an unsafe production change.

## Files changed

- `.gitea/backlog/clros-issues.json`
  - expands M01 from 27 to 32 children;
  - adds full issue contracts for M01-028 through M01-032;
  - serializes overlapping paging ownership;
  - updates M01-025's prerequisites and evidence boundary; and
  - preserves M01-026 as the real-QEMU proof owner.
- `scripts/sync-gitea-backlog.ps1`
  - updates the exact structural contract to 321 children and 337 issues.
- `tests/OS.HostTests/GiteaBacklogManifestTests.cs`
  - freezes the new totals, order, ownership, dependency graph, host-only
    evidence, and offline-sync output.
- `tests/OS.HostTests/M01LivePagingPrerequisiteManifestTests.cs`
  - emits the M01-028 markers;
  - validates the graph, collision serialization, issue contracts, explicit
    live-evidence denials, and durable blocker ledger; and
  - avoids coupling later implementations to brittle source substrings or
    making blocker checks disappear solely from a desired-state change.
- `tests/OS.HostTests/RepositoryRecoveryTests.cs`
  - updates the shared M00-001 backlog invariant from 332 to 337 issues.
- `docs/boot.md`
  - records the exact feasibility blockers, repair chain, and QEMU boundary.
- `docs/architecture.md`
  - records the preparation, physical-arena, identity, call-gate, and evidence
    separation.
- `docs/repo-audit.md`
  - records 321/337/421 as current and 315/331/415 plus 316/332/416 as
    historical snapshots.
- `docs/issue-labels.md`
  - records the post-recovery readiness chain and administrative-only scope.
- `done/step873.md`
  - preserves this evidence ledger.

No production kernel, EFI, std, application, project, build, QEMU, README,
completion-gate, or reference source was changed.

## Tests run

- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\sync-gitea-backlog.ps1 -Offline`
  - passed with:

    ```text
    SYNC_MANIFEST_VALID milestones=16 labels=68 epics=16 children=321 issues=337
    SYNC_SUMMARY created=0 updated=0 unchanged=0 skipped=421 failed=0 wouldCreate=0 wouldUpdate=0
    TEST_PASS gitea.backlog.sync
    ```

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
  --disable-build-servers`
  - passed with zero warnings and zero errors.
- Complete built host harness:

  ```text
  C:\Projects\CLROS-M01-025\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe
  ```

  - ran from `2026-07-27T22:58:34.5032403Z` through
    `2026-07-27T23:06:19.3493822Z`;
  - completed with 3,222 `TEST_PASS` lines, zero `TEST_FAIL` lines, exactly
    one `TEST_PASS host.tests`, and zero stderr bytes;
  - emitted `CLROS_M01_028_READY` and `TEST_PASS m01.028` exactly once;
  - produced a 411,215-byte, 6,470-line stdout log with SHA-256
    `EB94E3962B79ED401B6B96DB2DA9974E96A662FA2AAF4C70525819C3A3661D6B`;
  - produced an empty stderr log with SHA-256
    `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.
- `dotnet build .\CLROS.slnx --nologo -m:1 --disable-build-servers`
  - passed with zero warnings and zero errors.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\check-naming.ps1`
  - passed `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\run-qemu-smoke.ps1 -DryRun`
  - passed `TEST_PASS qemu.runner.dryrun`.
- `git diff --check`
  - passed.
- A first-party source scan found no `.c`, `.cpp`, `.h`, `.asm`, or `.s`
  files under `src`, `std`, or `apps`.

## Evidence

The accepted ignored host log is:

```text
artifacts/logs/m01-028-final-reviewed/20260727T185834Z/host-tests.stdout.log
```

Its issue-specific terminal is:

```text
TEST_START m01.028
CLROS_M01_028_READY
TEST_PASS m01.028
```

M01-028 changes the durable dependency and evidence model only. The host tests
bind the durable issue ledger to the audited blocker taxonomy and prevent any
of M01-028 through M01-032 from carrying a real-QEMU/provider evidence label or
real-QEMU command.

Tracker synchronization and reviewed-closure details are recorded in the
follow-up evidence commit after the implementation commit is available.

## Failures

- An agent's first development harness invocation exceeded its tool timeout;
  the exact child process from this worktree was identified and stopped. No
  truncated output was accepted as evidence.
- The first complete parent-run attempt exposed the already documented
  line-ending-sensitive M01-023 source test because unchanged `EfiEntry.cs`
  had CRLF working-tree bytes. The file was mechanically normalized to its
  index-identical LF bytes; it has no Git diff.
- The next run exposed that the new evidence-boundary test required an explicit
  denial phrase while the five issue bodies used several semantically
  equivalent phrasings. All five boundaries now state that they do not prove
  live QEMU/OVMF execution.
- The following run exposed the shared M00-001 invariant's stale 332-issue
  count. It now verifies the same 337-issue total as the synchronizer and
  dedicated manifest tests.
- Independent review then found that the synchronizer's per-milestone table
  still stopped at M01-027 even though its aggregate count was current. The
  table now requires M01-001 through M01-032, and an adversarial test replaces
  M01-032 with dependency-valid M01-999 to prove the gap is rejected.
- Review also found that the initial required-command list omitted live
  self-test/preview/apply/convergence and that raw source-substring checks
  became vacuous or circular on future desired-state changes. The commands are
  now explicit, and the focused test validates the durable blocker ledger
  while each implementation issue owns its positive behavioral tests.
- A fresh complete run after all review-driven fixes passed and is the only
  accepted host-harness evidence for this step.

## Deferred work

- M01-029 is the next leaf after reviewed M01-028 closure. It owns the
  preparation-only API and must prevent synthetic event transcripts from
  satisfying executable readiness.
- M01-030, M01-031, and M01-032 remain blocked and serialized behind M01-029.
- M01-025 remains blocked until all four prerequisites close with reviewed host
  evidence.
- M01-026 retains the first cumulative real-QEMU/OVMF paging proof.
- No firmware allocation, page-table materialization, generated-code
  invocation, CR3/CR4/RFLAGS/PTE/TLB mutation, live W^X transition, page-zero
  hardware check, ExitBootServices, owned COM1, descriptor/fault handling,
  device I/O, application execution, hosted .NET, Kestrel, AWS, Azure, or
  completion-gate advancement is claimed.
