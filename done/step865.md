# Step 865 - Firmware-arena allocator stage-two integration

## Summary

Implemented M01-018 as a fail-closed, host-tested stage-two coordinator over
the existing M01 boot-memory contracts. The new no-stdlib-compatible core
accepts only a final M01-017 loaded-image/owned-stack state, one explicitly
successful EFI `LoaderData` arena record, a caller-attested refreshed owned
memory map, and a canonical reservation ledger containing that exact arena.

After validating all declared metadata spans and retained stage-one ownership,
the coordinator creates a free list solely from the allocator arena, performs
exactly one 4 KiB metadata allocation, proves the returned page is the arena's
first page, frees it with the original restore receipt, and requires a second
free to fail with `FreeRangeNotAllocated` without changing state or the node.
The caller-owned durable result is committed only after the complete cycle and
final result validation succeed.

Five value-only phase auditors make source/subtraction, state materialization,
allocation containment, free accounting, and terminal double-free rejection
independently testable with real leaf-operation results. A separate host
presenter emits the exact nine-line `m01.stage2` transcript retrospectively;
the default boot plan and `BootSequence.Run` path remain unchanged.

## Why

M01-017 established the loaded-image and owned-stack stage-one boundary, while
M01-002, M01-006, M01-007, M01-008, and M01-009 supplied individually reviewed
reservation and allocator leaves. No prior coordinator proved that a
firmware-allocated loader-owned arena, refreshed map evidence, canonical
ownership ledger, free-list materialization, allocation, restoration, and
double-free rejection compose in one bounded sequence.

Treating the `AllocatorArena` ledger entry as an ordinary exclusion would
subtract or reject the allocator's complete source. M01-018 instead validates
the complete canonical ledger first, proves every other reservation disjoint
from the arena, then uses the exact arena as the sole source with an empty
operational exclusion table. This preserves the arena record as ownership
metadata without accidentally making the owned arena unusable.

The declared capacities on the stage-one and output static-state pointers are
also treated as complete address spans, not minimum-size hints. Wrapping
capacities and capacity tails that alias another metadata table, either map
buffer, or the arena are rejected before dereference or output mutation.

## Files changed

- `src/OS.Kernel/Boot/M01LiveAllocatorIntegration.cs`
- `src/OS.Kernel/Boot/BootSequence.cs`
- `src/OS.Kernel/Boot/BootSequencePlan.cs`
- `tests/OS.HostTests/M01LiveAllocatorIntegrationTests.cs`
- `tests/OS.HostTests/BootSequencePlanTests.cs`
- `docs/boot.md`
- `done/step865.md`

No change was made to `src/OS/Boot/EfiEntry.cs`, `src/OS/OS.csproj`,
`src/OS.Kernel/Diagnostics/Probes.cs`, EFI/QEMU scripts,
`docs/completion-gates.md`, `README.md`, or any reference snapshot.

## Tests run

All commands were run from `C:\Projects\CLROS-M01-018` against base commit
`d4bd8ce438f45c62ae029ded837863bbf7d08fa4`:

```text
dotnet build .\CLROS.slnx --nologo -m:1
  exit 0; Build succeeded; 0 warnings; 0 errors

dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
  exit 0; Build succeeded; 0 warnings; 0 errors

dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
  exit 0
  line 2208: TEST_START m01.018
  line 2239: CLROS_M01_018_READY
  line 2240: TEST_PASS m01.018
  line 6284: TEST_PASS host.tests
  all 15 named M01-018 subtests plus the aggregate terminal passed
  zero TEST_FAIL or OS_SMOKE_FAIL markers
  standard error was empty

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
  exit 0
  TEST_PASS naming.guard
  TEST_PASS source.firewall

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun
  exit 0
  TEST_PASS workload.profile.tooling default single
  TEST_PASS qemu.runner.dryrun

git diff --check
git diff --cached --check
  exit 0; no whitespace errors

git status --short
  exit 0; exactly the seven expected M01-018 implementation, test,
  documentation, and evidence files were staged
```

The authoritative full host-harness run produced 399,781 bytes of standard
output and zero bytes of standard error. Each aggregate M01-018 marker and the
terminal `TEST_PASS host.tests` occurred exactly once.

## Evidence

The coordinator requires and cross-binds:

- an initialized M01-017 static state at
  `ContinuationRspContained`, with a `ContinuationObserved` one-shot switch
  state and an `Active` owned-stack lifetime;
- exact embedded handoff, switch-state, and stack-layout offsets plus the
  complete stage-one POD size;
- a unique loaded-image reservation containing the complete stage-one POD and
  planned continuation address;
- exact switch-stub, guard, active-stack, and allocator-arena reservations;
- exactly one successful, identity-mapped EFI `LoaderData` arena allocation of
  2 through 4096 aligned 4 KiB pages with redundant byte/end accounting;
- changed map key, exactly incremented caller map generation, and strict
  arena-allocation, map-capture, and ledger-build ordinals;
- disjoint stage-one, allocation-table, ledger, output-state, owned-map,
  source-map, and arena spans, using complete declared capacities;
- one identity subtraction result with zero removed pages/bytes and one exact
  output range;
- one exact free-list node and allocator state over the complete arena;
- one restorable, one-node, zero-reservation, first-page 4 KiB allocation with
  exact source receipt, state accounting, and suffix node;
- one exact `Next` coalescing free that restores the original state and node;
  and
- one exact second-free failure bound to the original arena and allocation,
  with unchanged state/node and exact failure-call metadata.

Every rejected execution leaves caller-owned stage-two output byte-identical.
On success, the final result is validated before the single output commit. A
second call against a completed state returns `DuplicatePhase`.

The public phase auditors are POD/value-only and mutation-free. Host coverage
feeds them default and failed subtraction/materialization results, allocation
and free failures, mismatched accounting, and a genuinely successful
allocation produced from a separate arena. Both the allocation and terminal
double-free auditors reject that valid-but-foreign evidence.

The exact no-stdlib compiler closure verifies that the core and its required
boot/memory contracts compile under `NOSTDLIB_EFI`, while PE metadata inspection
requires the six intended public methods and rejects assembly references,
module references, import mappings, P/Invoke methods, and external runtime
metadata. Source guards also reject strings, spans, exceptions, reflection,
dynamic invocation, tasks, arrays, managed-reference fields, and host-only
fallbacks in the production core.

The independent packed success plan is exactly:

```text
TEST_START m01.stage2
M01_ALLOCATOR_ARENA_ALLOCATED
M01_STAGE2_MAP_RECAPTURED
M01_RESERVATIONS_APPLIED
M01_LIVE_ALLOCATED_4K
M01_LIVE_PAGE_WITHIN_ARENA
M01_LIVE_FREED_4K
M01_DOUBLE_FREE_REJECTED
TEST_PASS m01.stage2
```

The presenter emits all nine lines only for a final successful result. Any
ordinary failure emits the stage-two start plus one status/checkpoint
`TEST_FAIL m01.stage2` and one `M01_STAGE2_FAIL`, suppressing all later success
lines. A nonfinal result carrying `Success` is reclassified as
`IllegalCoordinatorState`. The default five non-smoke/eight total boot lines
remain byte-for-byte unchanged.

Final log hashes:

```text
4F1BEBD1EE87CC34C7C5C36E4A174924B5958865FE17574139FB307385A0A38A  artifacts/m01-018-solution-build.log
B58CE455F4CC4A8FFFB002D014AD18C9166D5FB151FD99B1B94C8F8FE7BEB948  artifacts/m01-018-hosttests-build.log
9D2391E964F3B71D3FD2199F4C1D1CC3FF03557A4445A15C1FCE5316192C09F2  artifacts/m01-018-hosttests-final.out.log
E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855  artifacts/m01-018-hosttests-final.err.log
FB9E2F9DEB178474FD6713AA598348A053B28FC79CA27732159CDD0AC565CE18  artifacts/m01-018-check-naming.log
FD277F0955964B3A7F92EEC4BF56E94E53A5AE520F166F52EC71E4EECB92005B  artifacts/m01-018-qemu-dryrun.log
```

Reference influence:

- `C:\Projects\CLROS\references\SharpOS-main\OS\src\Boot\ExitBootServicesProbe.cs`
  was reviewed only for the ordering concept that a firmware arena allocation
  must precede the refreshed memory-map capture.
- The inspected snapshot file has SHA-256
  `65B892A8BD752C95077E17386FD127F040A3A7F013E968485ACCB2EC4FF24DA1`.
- No firmware call adapter, map retry loop, platform coupling, console path, or
  post-`ExitBootServices` behavior was copied.
- Source study is owner-authorized. The local snapshot's exact upstream
  revision remains unknown, so this evidence records the inspected file hash
  instead of inventing provenance.

Two independent final reviews found no remaining production, presenter,
documentation, scope, or no-stdlib defect after their findings were corrected.

## Failures

Several pre-evidence failures were found and corrected:

- The first composition draft risked treating the arena's ownership record as
  its own allocator exclusion. Canonical-ledger validation was separated from
  the empty operational exclusion list.
- Initial compilation exposed the actual owned-switch-state property name and
  explicit reserved-field initialization requirements; both were corrected.
- Early alias helpers were inserted in the wrong type during refactoring and
  produced compiler errors. They were moved into the coordinator.
- The first focused run found two test-harness assumptions: arena alias setup
  occurred after output-state mutation, and one source assertion was
  whitespace-sensitive. The fixture ordering and assertion were corrected.
- One concurrent diagnostic run held host-test output assemblies open. The
  superseded process was stopped and all authoritative builds/runs were then
  serialized.
- Adversarial review found that the first capacity checks covered only the
  required struct size. Full declared capacities, wrap checks, and capacity
  tail alias tests were added.
- The same review found incomplete cross-binding of stage-one embedded records,
  the loaded-image reservation, map/metadata spans, restored free state, and
  final output atomicity. Each relationship is now explicit.
- A final review demonstrated that accounting-equivalent allocation evidence
  from another arena could satisfy an under-bound terminal audit. The audit
  now receives and exactly binds the arena, original allocation/receipt,
  restored state/node, and failure metadata. A real foreign-arena allocation
  is retained as a regression test.

All superseded runs were discarded as completion evidence. The final preserved
run and every common repository gate passed.

## Deferred work

- M01-019 owns reversible owned-paging integration over the existing page-table
  arena, identity-map, CR3-stub, protection, TLB, and restoration leaves.
- M01-020 owns explicit source linkage into the EFI project.
- M01-021 and later issues own live `AllocatePages`, serialized refreshed-map
  capture, `EfiEntry` wiring, actual stack transfer, page protection, default
  promotion, and cumulative real-QEMU/OVMF evidence.
- The previous map key/generation and the relationship between the supplied
  refreshed-map metadata and ledger remain caller attestations. M01-018 does
  not parse live descriptor bytes or independently observe firmware.

## Evidence boundary

- This is host evidence over caller-supplied pre-EBS metadata only.
- The coordinator is not linked into `src/OS`, called from `EfiEntry`, or used
  by the default boot path.
- No EFI artifact was rebuilt or inspected for this issue.
- The QEMU command was a dry run; QEMU/OVMF did not execute.
- No live EFI `AllocatePages`, memory-map recapture, physical-page access,
  firmware exit, post-EBS allocator, stack transfer, CR3 load, TLB operation,
  page-protection change, NX/W^X enforcement, or guest marker occurred.
- `CLROS_M01_018_READY` and the `m01.stage2` transcript prove host coordinator
  ordering, arena confinement, accounting, and marker suppression only. They
  do not change a completion gate or prove M01-019+, M02, real firmware, or any
  cloud boundary.
