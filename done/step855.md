# Step 855 - M01-008 Typed One-Page Allocator Metadata Mutation

## Summary

Implemented the host-only M01-008 boundary that mutates caller-owned boot-page
allocator metadata for exactly one aligned 4 KiB result while retaining the
explicit `PerformsLiveAllocation=false` evidence boundary.

The change adds:

- A readonly unmanaged `BootPageAllocatorOnePageResult` that fails closed by
  default and carries typed `PhysicalAddress` bounds, exact one-page/4096-byte
  geometry, before/after allocator states and accounting, caller-declared
  counts and capacities, changed/inserted/visited node indexes, and nested
  reservation/address evidence.
- An unsafe no-stdlib `AllocateOnePage` API over
  `BootPageAllocatorCoreContracts` PODs, a caller-owned node
  pointer/count/capacity, a read-only canonical-reservation
  pointer/count/capacity, and explicit page-count/alignment inputs.
- Append-only mutation status values for compiler-layout mismatch, node and
  reservation table multiplication/address overflow, count and capacity
  bounds, missing/misaligned storage, table aliasing, state accounting,
  malformed/unsorted/overlapping reservations, reservation/free-range
  overlap, allocated-count overflow, and typed-address construction failure.
  Existing values 0 through 18 remain unchanged.
- Complete preflight before the first write: exact 4096-byte state policy,
  exactly one requested page, power-of-two page alignment, compiler-sized
  table spans, maximum counts, count/capacity relationships, null/alignment
  checks, full declared-span alias rejection, state accounting, canonical
  reservation shape/order, complete free-list geometry/order/chain/totals,
  and reservation exclusion.
- Deterministic first-fit mutation for range-head, range-tail, exact-range,
  and middle-split cases, including exact non-head removal and predecessor
  relinking.
- A split policy that may claim only an exact all-zero slot inside logical
  `nodeCount`; it never scans the capacity-only tail or overwrites stale
  unlinked metadata.
- Typed address construction and all remaining failure checks before the
  single mutation phase. Every failure returns the original state and leaves
  the complete caller-provided backing tables byte-identical.
- Representability checks on successful result evidence so default, partially
  initialized, arithmetically impossible, inactive-index, or coordinated
  forged summaries cannot report success.
- The historical Span-shaped `Allocate`/`Free` surface retained only under
  `#if !NOSTDLIB_EFI`. Canonical documentation now states that it is a
  host-compatibility helper, not an authoritative transition interoperable
  with the new pointer core and not evidence of safe/live freeing.
- Hostile fabricated-pointer vectors isolated in disposable child processes.
  A timeout, access violation, abnormal exit, or wrong status therefore
  becomes a stable parent-harness failure rather than terminating the main
  process before `TEST_FAIL m01.008`.
- A Release-configuration evaluated-EFI firewall that queries `Compile`,
  `ProjectReference`, and `Reference`, requires the managed-reference lists to
  remain empty, and rejects the allocator closure from the canonical EFI
  project.
- An isolated exact-source `NOSTDLIB_EFI` build with framework references
  removed and zero emitted PE assembly references.

The host harness emits:

```text
TEST_START m01.008
CLROS_M01_008_READY
TEST_PASS m01.008
```

## Why

M01-006 materialized validated allocator-state PODs and M01-007 supplied the
typed reservation-subtraction transform, but the existing mutator still
exposed a host-oriented Span/raw-address result and did not provide a focused
reservation-aware one-page pointer contract suitable for later no-stdlib EFI
source wiring.

M01-008 supplies that independently testable metadata transition without
claiming physical-page ownership. It deliberately accepts caller-provided
state and storage, returns a proposed typed range plus replacement state, and
requires the caller to keep the declared spans mapped, stable, and
exclusively serialized for the entire operation.

## Files changed

- `src/OS.Kernel/Memory/BootPageAllocatorMutation.cs`
- `tests/OS.HostTests/BootPageAllocatorOnePageAllocationTests.cs`
- `docs/boot.md`
- `docs/architecture.md`
- `done/step855.md`

No forbidden integration file, `src/OS/Boot/EfiEntry.cs`,
`src/OS/OS.csproj`, `BootSequence.cs`, `Probes.cs`, `README.md`,
`docs/completion-gates.md`, reference snapshot, or generated artifact changed.

## Tests run

All verification commands in this section ran from
`C:\Projects\CLROS-M01-008`.

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
```

Passed serially with 0 warnings and 0 errors.

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
```

Passed serially with 0 warnings and 0 errors.

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

Passed with exit code 0. The final unfiltered stdout log contains 5,999 lines,
exactly one `TEST_PASS host.tests`, and zero line-leading failure markers
matching `^TEST_FAIL(?:\s|$)`. It contains exactly one each of:

```text
TEST_START m01.008
CLROS_M01_008_READY
TEST_PASS m01.008
TEST_PASS host.tests
```

The 25 focused M01-008 groups passed:

- Stable append-only statuses, readonly/sequential unmanaged contracts, and
  default-result failure.
- One-page allocation from a free-range head.
- One-page allocation from a free-range tail.
- Exact head and exact non-head range consumption, predecessor relinking, and
  head preservation.
- Middle-range split with an exact-default logical slot.
- Exact typed range, page/byte, state, count, capacity, and index evidence.
- Canonical reservation adjacency, logical-count-only reads, immutability, and
  capacity-tail preservation.
- Mutation of only the caller-owned logical node prefix.
- Default, corrupt, and arithmetically inconsistent allocator states.
- Zero/wrong page size, zero/wrong page count, invalid alignment, and
  alignment-up address overflow.
- Malformed, unsorted, overlapping, overflowing, and invalid-provenance nodes.
- Invalid head/next indexes, short/disconnected chains, self/multi-node
  cycles, and node-count mismatch.
- Page/byte total mismatch and allocated-count overflow.
- Default, malformed, unsorted, overlapping, and capacity-tail reservation
  records.
- Every active-free-range/canonical-reservation overlap and exact adjacency.
- No suitable range, logical split-slot exhaustion, stale unlinked metadata,
  and capacity-only exact-default slot rejection.
- Node and reservation count/capacity maximums, multiplication overflow,
  missing/misaligned pointers, declared address-span wrap, and safe
  zero-count/null behavior.
- The hostile pointer vectors above in independently bounded child processes.
- Same-base and declared-capacity-tail node/reservation table alias rejection.
- Byte-for-byte failure atomicity across reachable failure classes.
- Deterministic semantic result/state equality and allocation-free warmed
  execution.
- Coordinated forged-success rejection, including impossible accounting,
  inactive logical indexes, invalid insertion topology, and forged
  initialization.
- Historical host Span compatibility remaining behind the host-only compiler
  branch.
- Source lexical firewalls plus evaluated Release EFI `Compile`,
  `ProjectReference`, and `Reference` exclusion.
- Exact no-stdlib source closure compilation with zero assembly references.
- Repository-root and recursive first-party marker uniqueness with
  failure-gate -> throw -> ready-marker -> pass-marker source ordering.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
```

Passed:

```text
TEST_PASS naming.guard
TEST_PASS source.firewall
```

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun
```

Passed for `SingleMinimalApi` with `TEST_PASS qemu.runner.dryrun`. This is only
runner/command/marker preflight and is not guest boot evidence.

```powershell
git diff --check
git status --short
```

`git diff --check` passed. The final pre-commit status contained only the five
declared files above. Git emitted line-ending conversion warnings for existing
files and reported no whitespace error.

An explicit first-party extension scan found no `.c`, `.cc`, `.cpp`, `.cxx`,
`.h`, `.hpp`, `.asm`, or `.s` file under `src`, `std`, or `apps`. A
changed-file credential-pattern scan also passed.

## Evidence

Final tool and source state:

- .NET SDK: `10.0.302`
- Git: `2.54.0.windows.1`
- Windows PowerShell: `5.1.26100.8894`
- Branch: `codex/M01-008-typed-page-allocation`
- Base commit: `7b37c896239174f6f412ff9551b47ca34d2d40f2`

Final production, test, and canonical-documentation hashes:

- `BootPageAllocatorMutation.cs`:
  `BBFF8D7F9D07F621B13963856E835D6C51AF8B7DED7038B5AE1BB10837F1FC6E`
- `BootPageAllocatorOnePageAllocationTests.cs`:
  `3FAE30BE945D6C0C2655F1673CAE54BFE61D629A47FB6E8DFDD0404C686DAF37`
- `docs/boot.md`:
  `DB28655884B3D3678B0A4E77B30668014EC6036E9CFB36D4A731749490CA2C2F`
- `docs/architecture.md`:
  `66FE15A03B02071C250CCA355B26E22AF70B0D8DDD727ED7FDBAA37787258337`

The final unfiltered host run completed at
`2026-07-26T07:43:31.3310456Z`:

- Development-host path:
  `C:\Users\Roger\AppData\Local\Temp\clros-m01-008-final-20260726T073817241Z.stdout.log`
- SHA-256:
  `FF849EB2177F141977E5BFED11826A7CF379385D0CF53EB96D1204B0449ADD10`
- Bytes: `766346`
- Lines: `5999`
- Line-leading `^TEST_FAIL(?:\s|$)` count: `0`
- Process exit code: `0`

The redirected stderr file was empty:

- Development-host path:
  `C:\Users\Roger\AppData\Local\Temp\clros-m01-008-final-20260726T073817241Z.stderr.log`
- SHA-256:
  `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`
- Bytes: `0`

The implementation and tests were independently authored after reviewing
these owner-authorized snapshots as test-only behavioral influences:

- `C:\Projects\CLROS\references\abanu-master\src\Abanu.Kernel.Core\MemoryManagement\PageAllocators\SimplePageAllocator.cs`
  - SHA-256:
    `FC4CF741D501CB2040C7BAC1F40E945CD4F8EB865D9D4C88F82B4E3127B7282D`
- `C:\Projects\CLROS\references\nativeaot-patcher-main\src\Cosmos.Kernel.Core\Memory\PageAllocator.cs`
  - SHA-256:
    `EC216B54C61765DC83F7CD6945614C2D97BFBDA6D2925668B12C4239D2E2368B`

Abanu informed independently authored exhaustion, boundary-page,
fragmentation, and allocation-order vectors. The nativeaot-patcher snapshot
informed independently authored single-page failure, capacity, accounting,
and double-use threat vectors. No reference implementation was copied, built,
linked, or imported, and the reference snapshots remained read-only.

Three independent read-only reviews audited production, tests, no-stdlib/EFI
firewalls, marker semantics, and documentation. The final reviews found no
remaining actionable M01-008 defect.

## Failures

- Several early full-harness attempts were stopped when source, tests, or
  documentation changed during independent review. No partial or stale run was
  used as final evidence.
- Review found that the first state validator admitted a coordinated
  impossible summary with more free nodes than free pages. Production now
  rejects that accounting, and direct-state plus forged-result regressions
  cover it.
- Review found that a forged result could name an inactive logical slot when
  only one active node existed. Representability validation now binds that
  case to the actual head, and the exact valid-but-inactive forge is covered.
- Review found a tautological result-size assertion, raw-padding determinism
  comparison, missing exact non-head allocation coverage, capacity-tail
  canaries that were too structurally valid, incomplete root marker scanning,
  and weak marker-order checks. The final tests use an unmanaged compile-time
  constraint, semantic field comparisons, a non-head exact allocation,
  malformed/default capacity tails, repository-root scanning, and explicit
  success-only source ordering.
- Review found that the first evaluated EFI firewall inspected default
  configuration `Compile` items only. The final firewall evaluates Release
  `Compile`, `ProjectReference`, and `Reference` items and rejects all managed
  references plus the forbidden allocator sources.
- Review found that fabricated raw pointers executed in the main harness and
  could erase stable failure evidence if validation ordering regressed. All
  hostile vectors now execute in separately bounded child processes.
- The first subprocess cleanup prefix did not match the cleanup guard and
  caused focused group 17 to fail with
  `Refusing to delete an unexpected M01-008 workspace.` Both workspace
  creators now use the exact guarded leaf pattern.
- An alignment-up overflow test initially asserted only the outer mutation
  status and expected the wrong default nested address status. It now requires
  `AddressOverflow`; focused group 16 passes.
- Review found that the first split-exhaustion vector put nondefault metadata
  in a capacity-only slot, allowing an incorrect capacity scan to false-green.
  The final vector puts an exact-default record outside `nodeCount`: an
  incorrect implementation would claim it and fail both the status and
  byte-preservation assertions.
- One parallel solution/host-test build attempt raced their shared `obj`
  outputs and failed with missing reference assemblies. The required builds
  were rerun serially and passed.
- A later solution-build attempt encountered host-test output DLLs locked by
  an independent audit process. The superseded audit process tree was stopped;
  the required serial solution and host-test builds then passed with zero
  warnings and errors.

## Deferred work

This step proves only deterministic host-tested mutation of caller-owned
allocator metadata for one typed 4 KiB result.

It does not:

- Link `BootPageAllocatorMutation.cs` into `src/OS/OS.csproj` or invoke it from
  `EfiEntry`.
- Prove that any supplied range is explicitly firmware-allocated CLROS
  property or install a live authoritative allocator.
- Allocate, reserve, claim ownership of, zero, map, protect, access, or free a
  physical page.
- Provide a safe long-lived free/ownership token, reject live double free, or
  coalesce authoritative live ranges. M01-009 owns the next host-only typed
  free/coalescing slice.
- Prove caller exclusivity, storage validity, concurrency serialization, or
  ABA protection beyond the documented preconditions.
- Read or mutate the live UEFI map, call `ExitBootServices`, configure paging,
  load CR3, enforce NX/W^X, or run after firmware exit.
- Run this allocator source under QEMU/OVMF or provide guest, firmware,
  paging, hardware, storage, networking, runtime, application, Kestrel, or
  cloud-provider evidence.
- Change `docs/completion-gates.md` or close a live boot-memory gate.

M01-023 later owns source wiring and application inside an explicitly
firmware-allocated pre-EBS arena. M01-024 owns the corresponding real-QEMU
allocation/free/double-free evidence.
