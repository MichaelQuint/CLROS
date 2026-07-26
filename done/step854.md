# Step 854 - M01-007 Typed Boot Reservation Subtractor

## Summary

Implemented the host-only M01-007 boundary that subtracts canonical typed boot
reservations from caller-owned typed source ranges without importing the
existing `Span<T>`-shaped host planners into the no-stdlib EFI closure.

The change adds:

- An exact 40-byte readonly unmanaged `BootReservationSourceRange` POD with
  typed half-open physical bounds, redundant checked page/byte metadata, and
  exact zero-based source provenance.
- A bounded unsafe pointer/count/capacity subtraction API that accepts one
  through 4096 source ranges, zero through 256 reservations, and no more than
  4096 output fragments.
- Complete declared-capacity byte-span validation using compiler `sizeof`
  values before the first input dereference.
- Null, alignment, multiplication-wrap, address-wrap, capacity, and pairwise
  source/reservation/destination alias rejection.
- Exact source-ordinal, structural, sorted/disjoint, adjacency, and
  one-source reservation-containment validation.
- A write-free subtraction dry run that validates every proposed fragment and
  computes exact required-output, removed, and remaining page/byte totals.
- Destination preflight after the dry run and one structurally infallible
  emission phase with no failure branch after the first destination write.
- Zero-reservation identity behavior and complete-cover success with a null,
  zero-capacity destination.
- Value-only result evidence that fails closed by default, retains no caller
  pointer, rechecks compiler layouts and exact accounting, and rejects
  impossible source/reservation/output topology summaries.
- Explicit caller preconditions that all declared unsafe storage remains
  valid, exclusively owned, and stable throughout validation, dry run, and
  emission.

The host harness emits:

```text
TEST_START m01.007
CLROS_M01_007_READY
TEST_PASS m01.007
```

## Why

The live EFI path currently normalizes conventional-memory metadata, but it
does not have a focused no-stdlib contract for applying the canonical CLROS
reservation ledger to a caller-owned arena. Existing
`BootMemoryRegionPlan.cs` and `BootPageAllocatorPlan.cs` are useful host
planners, but their `Span<T>` surface must not become a prerequisite of the
explicit no-stdlib EFI project.

M01-007 provides the independently testable transform needed before later
integration. It deliberately operates only on supplied metadata. It does not
assert that any described page is mapped, firmware-allocated CLROS property,
reserved, or owned.

## Files changed

- `src/OS.Kernel/Memory/BootReservationSubtractor.cs`
- `tests/OS.HostTests/BootReservationSubtractorTests.cs`
- `docs/boot.md`
- `docs/architecture.md`
- `done/step854.md`

No forbidden integration file, `src/OS/OS.csproj`, `EfiEntry.cs`,
`BootSequence.cs`, `Probes.cs`, `BootMemoryRegionPlan.cs`,
`BootPageAllocatorPlan.cs`, `README.md`, completion-gate file, reference
snapshot, or generated artifact changed.

## Tests run

All verification commands in this section ran from
`C:\Projects\CLROS-M01-007`.

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
```

Passed with 0 warnings and 0 errors.

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
```

Passed with 0 warnings and 0 errors.

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

Passed with exit code 0. The final unfiltered stdout log contains 5,947 lines,
exactly one `TEST_PASS host.tests`, and zero line-leading failure markers
matching `^TEST_FAIL(?:\s|$)`. It contains exactly one each of:

```text
TEST_START m01.007
CLROS_M01_007_READY
TEST_PASS m01.007
TEST_PASS host.tests
CLROS_RUN_EXIT=0
```

The 25 focused M01-007 groups passed:

- Stable explicit status values and exact unmanaged layouts.
- Default-value fail-closed behavior.
- Positive typed source-range construction.
- Negative provenance, default-address, zero-page, alignment,
  multiplication-overflow, and address-wrap construction.
- Zero-reservation identity with exact declared capacities and unchanged
  destination suffix.
- Exact head subtraction.
- Exact tail subtraction.
- Exact middle split.
- Complete coverage with null, zero-capacity output.
- Multiple sources and reservations with exact order, provenance, capacities,
  and accounting.
- Adjacent source ranges remaining separate.
- Adjacent reservations producing no zero-length fragment.
- Malformed, unordered, duplicate, and overlapping source rejection.
- Malformed, unordered, duplicate, and overlapping reservation rejection.
- Before-source, after-source, partial, gap, gap-spanning, and cross-source
  reservation rejection.
- Every page-size, count, capacity, null, alignment, byte-span-overflow, and
  address-wrap preflight class with destination canary preservation.
- Same-base and declared-capacity-tail alias rejection for every table pair
  with full backing-storage preservation.
- Exact insufficient-destination reporting and atomic failure.
- Destination preservation for every reachable post-read failure.
- Exact-prefix-only destination mutation on success.
- Deterministic, allocation-free warmed execution.
- Correct result-field offsets, byte mutations, real internal-success factory
  calls, and coordinated impossible-summary rejection.
- Production lexical firewalls plus real-build evaluated EFI `Compile`-item
  exclusion of the subtractor and both forbidden host planners.
- Exact five-source `NOSTDLIB_EFI` compilation with framework references
  removed and zero emitted PE assembly references.
- Repository-wide first-party marker uniqueness with the marker remaining
  host-only and absent from guest production source.

The output-fragment boundary test constructs 4,097 required fragments and
proves deterministic `RequiredOutputRangeCountOverflow` at 4,096 without
modifying the destination.

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
```

Passed with 0 warnings and 0 errors.

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

Passed with the `SingleMinimalApi` profile and
`TEST_PASS qemu.runner.dryrun`. This is command/tooling preflight only and is
not guest boot evidence.

```powershell
git diff --check
git status --short
```

`git diff --check` passed. The final pre-commit status contained only the five
declared files above. Git emitted line-ending conversion warnings for the two
existing documentation files and reported no whitespace error.

An explicit first-party extension scan found no `.c`, `.cc`, `.cpp`, `.cxx`,
`.h`, `.hpp`, `.asm`, or `.s` file under `src`, `std`, or `apps`. A
changed-file credential-pattern scan also passed.

## Evidence

Final tool and source state:

- .NET SDK: `10.0.302`
- Git: `2.54.0.windows.1`
- Windows PowerShell: `5.1.26100.8894`
- Branch: `codex/M01-007-reservation-subtractor`
- Base commit: `b686bd7e532b53c83fa5abedcda4b38d3f70d0ad`

Final production, test, and documentation hashes:

- `BootReservationSubtractor.cs`:
  `B8D70A06DCA71B6ED63AA2F2A8242FD4044667FFB1B90495C6B9CF688A086D5A`
- `BootReservationSubtractorTests.cs`:
  `BAA949861D2AA909D2F1F947B492D78DF324EE31AE8727228B7FB661C7A039BE`
- `docs/boot.md`:
  `112A3977EE454F34C31EC9C2C3B70C973D83AA3207FFA7BD710190F908B0296F`
- `docs/architecture.md`:
  `DDC90D318322869394ACD65A13AA41EF162CEDE8998E139379325E36678C6716`

The final unfiltered host stdout log completed at
`2026-07-26T06:45:02.9541316Z`:

- Development-host path:
  `C:\Users\Roger\AppData\Local\Temp\clros-m01-007-final-20260726T064058360Z.stdout.log`
- SHA-256:
  `83140FE2336DF24C04B548882BC2367F3B11C5A39ECB9256BB25E892C94EF4DD`
- Bytes: `379987`
- Lines: `5947`
- Line-leading `^TEST_FAIL(?:\s|$)` count: `0`
- Exit marker: `CLROS_RUN_EXIT=0`

The separately redirected stderr file contains only Windows PowerShell
module-initialization progress records:

- SHA-256:
  `2CE70D6C8C9AD6726C794690C8B14A2B83673648CAC88EDF4FF1ACC50EC46348`
- Bytes: `616`

The implementation and tests were independently authored after reviewing
these owner-authorized snapshots as test-only influences:

- `C:\Projects\CLROS\references\RoseOS-master\Loader\src\MemoryMap.cs`
  - SHA-256:
    `CE90AE629B538E558B57B0BD315ED8B11DD559ED1A841368D109DE28D70B8B28`
- `C:\Projects\CLROS\references\abanu-master\src\Abanu.Kernel.Loader\BootMemory.cs`
  - SHA-256:
    `71AD5E7D908AA3655AC52CFA07CAB4F77181C71F6BADAFAD2A1BB70C80CBC640`

RoseOS informed negative coverage for fixed map buffers, missing reservation
accounting, and failure atomicity. Abanu informed negative coverage for fixed
bump assumptions, fragmentation, alignment, overflow, and capacity. No
reference implementation was copied, and the reference snapshots remained
read-only.

Two independent read-only reviews audited production and tests. The final
review found no remaining actionable M01-007 issue.

## Failures

- The initial test file was observed while still being authored and briefly
  failed compilation because its closing structure was incomplete. That
  intermediate state was not used as evidence.
- The first focused test runs exposed only test-expectation defects: one
  default structural status assumption and marker self-counting. Both were
  corrected before final evidence.
- A first captured full-harness attempt began before the final marker-test
  rebuild and was stopped. A later attempt was stopped when independent review
  identified additional hardening work. Neither partial run was used as
  evidence.
- Independent review found that the first result validation admitted
  coordinated impossible summaries. The final predicates enforce per-source,
  per-reservation, and per-output minimum page counts; upper and lower
  source/reservation/output topology bounds; and exact zero-reservation
  identity.
- Review found incomplete preflight failure-atomicity and same-base-only alias
  coverage. The final tests preserve destination or complete backing storage
  across every reachable failure and declared-capacity-tail alias.
- Review found that the first marker scan did not cover all first-party
  source/test/tool surfaces. The final scan covers `src`, `std`, `apps`,
  `tools`, `tests`, `scripts`, `docs`, and `.gitea`, while distinguishing the
  canonical host emitter from declarative documentation.
- Review found that a lexical keyword-plus-space check and a
  design-time-only MSBuild evaluation could false-green. The final test uses a
  token-boundary exception regex and evaluates actual-build `Compile` items.
- Review found that positive helpers did not assert exact declared
  capacities, and the evaluated EFI firewall omitted the two explicitly
  forbidden host planners. The final tests lock every successful capacity and
  reject `BootMemoryRegionPlan.cs` and `BootPageAllocatorPlan.cs`.

## Deferred work

This step proves only deterministic host-tested reservation subtraction over
caller-owned typed metadata.

It does not:

- Link the new leaf into `src/OS/OS.csproj` or invoke it from `EfiEntry`.
- Read, copy, classify, or mutate the live UEFI memory map.
- Prove that a supplied source range is an explicitly firmware-allocated
  CLROS arena.
- Populate the live reservation ledger or apply image, stack, map, ACPI,
  diagnostic, or page-table reservations to live memory.
- Install allocator state or allocate, free, reserve, map, protect, or own any
  physical page.
- Prove concurrent caller serialization beyond the documented exclusive,
  stable-storage precondition.
- Configure paging, enforce NX/W^X, or execute after `ExitBootServices`.
- Run under QEMU/OVMF or provide guest, firmware, paging, or hardware
  evidence.
- Change `docs/completion-gates.md` or close a live boot-memory gate.

M01-008 owns the next host-only one-page allocation slice. M01-023 later owns
application inside an explicitly firmware-allocated live arena, and M01-024
owns the corresponding real-QEMU evidence.
