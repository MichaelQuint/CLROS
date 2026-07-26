# Step 849 - Define and validate the canonical boot reservation ledger

## Summary

Implemented the host-only M01-002 boot reservation contract:

- Added explicit, append-only `BootReservationKind : byte` values from
  `Uninitialized=0` through `PageProtectionProbe=14`.
- Added a 40-byte readonly unmanaged `BootReservedRange` POD with typed
  `PhysicalAddress` start/end values, explicit zeroed reserved bytes, page
  count, byte length, and a checked status-returning factory.
- Added a bounded unsafe `BootReservationTableValidator.Validate` core over a
  caller-owned pointer/count/capacity table.
- Rejected zero and excessive counts, insufficient capacity, null and
  misaligned pointers, and a wrapping full table span before the first
  dereference.
- Validated every redundant range field, sorted/disjoint order, adjacency,
  deterministic duplicate/nested/partial-overlap conflicts, loaded-image
  duplicate metadata coverage, checked totals, and the optional exact
  guard-plus-stack arena.
- Kept validation nonmutating and allocation-free on both success and failure
  paths. Guard and stack records are copied during the first pass rather than
  reread later.
- Added a retained exact-source `NOSTDLIB_EFI` compiler closure whose emitted
  PE must contain zero assembly references.

## Why

M01-001 established typed address domains but did not define which physical
ranges CLROS owns during boot. M01-002 provides the canonical value-only ledger
and structural validator required before later issues capture the loaded EFI
image, define the owned boot stack, subtract reservations from conventional
memory, or allocate live pages.

This contract is intentionally separate from `BootMemoryReservedRange`, which
classifies non-conventional UEFI descriptors and may legitimately describe
page zero. A CLROS-owned reservation must instead be nonzero, page aligned,
nonempty, nonwrapping, and internally self-consistent.

## Files changed

- `src/OS.Kernel/Memory/BootReservedRange.cs`
- `src/OS.Kernel/Memory/BootReservationTableValidator.cs`
- `tests/OS.HostTests/BootReservationTableValidatorTests.cs`
- `docs/boot.md`
- `docs/architecture.md`
- `done/step849.md`

No `src/OS/OS.csproj`, `EfiEntry`, `BootSequence`, `Probes`, README,
completion-gate, backlog-manifest, reference-snapshot, or first-party native
source file was changed by the M01-002 implementation branch.

## Tests run

Tool and source identity:

```text
dotnet SDK: 10.0.302
branch: codex/M01-002-boot-reservations
base: e7b507fee530d8a33429ed9d516aeaf0e8592f90
implementation: 503e4941db34a4a2aa557f1e3d519c3c2774f477
BootReservedRange.cs SHA-256:
  E143D2955A08FC7D02E2422AB923B5AF013FCD20AF6B9BD6A4335767869C3D0C
BootReservationTableValidator.cs SHA-256:
  23ABD4D8B0B9F631374E3D7C4BB9D8B68912DF1A6C335EFE6A949F0B5ECFCBF1
BootReservationTableValidatorTests.cs SHA-256:
  DBCFA4F5EC08CE0E88C4809980B18B0CA6F692365B064A5B569E15107B4547ED
RoseOS test-only source SHA-256:
  CE90AE629B538E558B57B0BD315ED8B11DD559ED1A841368D109DE28D70B8B28
```

Commands:

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
exit 0; 0 warnings; 0 errors

2026-07-26T02:29:04.6749368Z
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
exit 0; completed 2026-07-26T02:32:38.8211994Z

2026-07-26T02:32:54.1497385Z
dotnet build .\CLROS.slnx --nologo -m:1
exit 0; 0 warnings; 0 errors

powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\check-naming.ps1
exit 0; TEST_PASS naming.guard; TEST_PASS source.firewall

powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\run-qemu-smoke.ps1 -DryRun
exit 0; TEST_PASS workload.profile.tooling default single;
TEST_PASS qemu.runner.dryrun

git diff --check
exit 0
```

The final complete host run produced:

```text
log: C:\Users\Roger\AppData\Local\Temp\
  clros-m01-002-final-acceptance.log
log bytes: 735686
log SHA-256:
  A1E73B9DACAD03D37F6D815909219CC32E25D68E1F1B3CF576E03783FBE3DD81
TEST_PASS marker count: 2873
TEST_FAIL/OS_SMOKE_FAIL marker count: 0
CLROS_M01_002_READY marker count: 1
TEST_PASS m01.002 marker count: 1
TEST_PASS host.tests marker count: 1
```

## Evidence

Focused evidence markers:

```text
TEST_START m01.002
TEST_PASS m01.002 reservation kinds are stable and append only
TEST_PASS m01.002 reservation records and results are unmanaged pods
TEST_PASS m01.002 range factory creates typed half-open ranges
TEST_PASS m01.002 range factory rejects default alignment and overflow
TEST_PASS m01.002 validator rejects malformed reservation records
TEST_PASS m01.002 validator accepts canonical sorted disjoint table
TEST_PASS m01.002 validator classifies ordering and overlap conflicts
TEST_PASS m01.002 loaded image rejects duplicate metadata coverage
TEST_PASS m01.002 guard and stack exactly account for modeled arena
TEST_PASS m01.002 pointer count and capacity fail before dereference
TEST_PASS m01.002 validator is deterministic nonmutating and allocation free
TEST_PASS m01.002 source excludes hosted runtime dependencies
TEST_PASS m01.002 exact sources compile under nostdlib efi
CLROS_M01_002_READY
TEST_PASS m01.002
TEST_PASS host.tests
```

The matrix includes:

- Every declared reservation kind, fixed values 0-14, declaration order,
  uniqueness, future append behavior, default kind, and unknown values 15 and
  255.
- Exact field offsets and sizes, readonly/unmanaged/value-only surfaces, exact
  production member allowlists, and named typed-address fields.
- Nonzero, unaligned, maximum-edge, byte-multiplication overflow, end-address
  overflow, mismatched redundant metadata, and corruption of each of the seven
  reserved bytes.
- An accepted exact 256-record table and rejected count 257, insufficient
  capacity, null pointer, misalignment, and an aligned near-maximum pointer
  whose count-sized span wraps before any load.
- Sorted disjoint ranges, adjacency, unsorted input, exact duplicates, same
  start, nesting in both directions, partial overlap, and deterministic prefix
  totals/indexes.
- Loaded-image containment of each image-covered metadata kind, external
  metadata, reverse ordering, and partial overlap.
- Guard/stack absence, valid adjacency and union accounting, missing pairs,
  reversed order, gaps, overlap, and duplicates.
- Byte-for-byte source immutability and zero managed allocations over 1,000
  warmed success and failure iterations.
- Exact source compilation with `NoStdLib=true`, framework references removed,
  `NOSTDLIB_EFI` defined, bounded process/stream waits, guarded temporary
  cleanup, and zero emitted assembly references.

Two independent read-only reviews found no remaining production, test,
documentation, pointer-safety, no-stdlib, marker, or cleanup blocker after the
review findings below were repaired.

`references/RoseOS-master/Loader/src/MemoryMap.cs` was used only as a
test-only reminder for unchecked size growth, narrowing, continuation after
failure, and ownership hazards. No RoseOS source, API, string, status name,
pointer design, or EFI call was copied.

## Failures

Intermediate failures were retained and repaired rather than hidden:

- The first focused run expected a nested-range status for a partial overlap;
  the fixture was corrected to require the deterministic partial-overlap
  status.
- The first isolated compiler fixture lacked compiler-required `UInt32`,
  `IntPtr`, and `UIntPtr` definitions. Those predefined shim types were added
  only to the temporary test project.
- A pre-existing M01-001 manifest evidence phrase had drifted and stopped the
  full harness after the M01-002 focused tests passed. The canonical manifest
  repair was reviewed and merged separately as PR #337 at
  `e7b507fee530d8a33429ed9d516aeaf0e8592f90`; the synchronizer then converged
  all 415 managed tracker objects.
- Production review found that count/capacity/null/alignment checks did not
  yet reject a wrapping full pointer span. A distinct pre-dereference status,
  checked span arithmetic, and forged near-maximum-pointer regression were
  added.
- Test review found that the original source firewall could allow new managed
  private surfaces through its no-stdlib shim. Anchored lexical checks and
  exact field/method/constructor/nested-type allowlists now fail closed.
- Test review also found missing exact-maximum, reserved-byte, failure-path
  allocation, enum-order, and API-surface cases. Each gap was added before the
  final run.
- An early strengthened lexical rule matched the legitimate
  `OS.Kernel.Memory` namespace. The rule was narrowed to reject actual
  `Memory<T>`/`ReadOnlyMemory<T>` use while preserving the namespace.
- Parallel read-only harnesses briefly locked host-test output files. Their
  owners stopped the duplicate runs, after which one clean build and one clean
  acceptance run completed.

All final required commands passed. The QEMU command was a dry run and is not
reported as guest boot evidence.

## Deferred work

M01-002 proves only a host-tested ownership-record format, structural table
validation, deterministic status/index/totals evidence, nonmutation,
allocation-free execution, and isolated exact-source no-stdlib compilation.

It does **not** prove or implement:

- Source linking into `src/OS/OS.csproj`, `EfiMain` execution, or QEMU guest
  execution.
- Live population of any reservation record or proof that omitted metadata is
  actually contained in the loaded image.
- Loaded-image protocol capture, EFI `LoaderCode`/`LoaderData` provenance, or
  refreshed memory-map generation.
- Owned stack allocation, guard-page enforcement, stack switching, reservation
  subtraction, page allocation/free, page tables, CR3, TLB invalidation,
  cache policy, NX, or W^X.
- Real `ExitBootServices`, owned diagnostics, live PCI/storage/networking,
  hosted .NET, Kestrel, cloud-provider evidence, CloudReady status, or
  production readiness.

M01-003 may now capture the loaded EFI image range through the loaded-image
protocol after tracker evidence review closes M01-002 and the backlog
synchronizer recomputes readiness.
