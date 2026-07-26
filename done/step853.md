# Step 853 - M01-006 Caller-Owned Boot Allocator State Materializer

## Summary

Implemented the host-only M01-006 boundary that validates a bounded,
caller-supplied boot-page free-list and atomically materializes its summary
into caller-owned storage.

The change:

- Extracts the existing unmanaged `BootPageFreeListNode` and
  `BootPageAllocatorState` POD contracts into one no-runtime-compatible source
  file without changing their public layouts.
- Adds a pointer/count/capacity materializer with no `Span<T>`, exception,
  allocation, reflection, plan-result, firmware-entry, or boot-sequence
  dependency.
- Validates the complete declared source and destination spans before the
  first dereference.
- Rejects null, unaligned, wrapping, undersized, overlapping, excessive, or
  inconsistent caller metadata.
- Enforces the exact 4096-byte page policy, bounded usable-range provenance,
  checked node arithmetic, sorted nonoverlapping physical ranges, a complete
  acyclic linked chain, and exact page/byte totals.
- Builds the state in a local value and performs exactly one destination write
  only after every validation succeeds.
- Returns deterministic value-only failure evidence and preserves the source
  table, destination record, and caller-owned capacity sentinels on every
  failure path.
- Revalidates advertised structure sizes against compiler `sizeof` values
  before traversal or mutation and in every claimed success summary.
- Preserves a strict host-only boundary: the leaf is neither linked into the
  EFI project nor invoked by `EfiEntry`.

The host harness emits:

```text
TEST_START m01.006
CLROS_M01_006_READY
TEST_PASS m01.006
```

## Why

The existing host initializer consumed a `Span<BootPageFreeListNode>` and a
host plan result. Those are useful host-test shapes but are not the narrow
pointer/count contract needed by the later no-stdlib EFI integration.

M01-006 separates the stable unmanaged records from their host-only builders
and introduces an independently testable materialization boundary. Later work
can transcode live EFI metadata into these typed records without importing
host planners into the EFI closure or mutating arbitrary firmware-owned
conventional memory.

The typed 48-byte node is intentionally not wire-compatible with the current
legacy six-`ulong` raw slots in `EfiEntry`. M01-023 must perform an explicit
checked provenance/link transcode; it must not reinterpret-cast those slots.

## Files changed

- `src/OS.Kernel/Memory/BootPageAllocatorCoreContracts.cs`
- `src/OS.Kernel/Memory/BootPageAllocatorStateMaterializer.cs`
- `src/OS.Kernel/Memory/BootPageAllocatorState.cs`
- `src/OS.Kernel/Memory/BootPageFreeListPlan.cs`
- `tests/OS.HostTests/BootPageAllocatorStateMaterializerTests.cs`
- `docs/boot.md`
- `docs/architecture.md`
- `done/step853.md`

The two existing source files changed only to remove the POD declarations now
owned by `BootPageAllocatorCoreContracts.cs`.

No forbidden integration file, `src/OS/OS.csproj`, `EfiEntry.cs`,
`BootSequence.cs`, `Probes.cs`, `README.md`, completion-gate file, reference
snapshot, or generated artifact changed.

## Tests run

All verification commands listed in this section ran from
`C:\Projects\CLROS-M01-006`.

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

Passed with exit code 0. The final unfiltered stdout log contains 5,894 lines
and zero line-leading `TEST_FAIL` terminal markers matching
`^TEST_FAIL(?:\s|$)`. It contains exactly one each of:

```text
TEST_START m01.006
CLROS_M01_006_READY
TEST_PASS m01.006
TEST_PASS host.tests
```

The 16 focused groups passed:

- Stable statuses, constants, unmanaged layouts, and actual sizes.
- Default-value fail-closed behavior.
- Nontrivial-head success and caller sentinel preservation.
- Compatibility with the retained host initializer.
- Poison-pointer, count, capacity, alignment, wrap, and overlap preflight.
- Page-size, source-range, and expected-total metadata validation.
- Malformed node and checked arithmetic classification.
- Head and next-index bounds.
- Self-cycle and multi-node cycle rejection.
- Short-chain and disconnected-node rejection.
- Provenance ordering, physical ordering, overlap, and adjacency behavior.
- Exact page and byte totals.
- Failure atomicity, deterministic results, and allocation-free execution.
- Source/integration firewall enforcement.
- Unique issue readiness markers.
- Exact two-production-source `NOSTDLIB_EFI` compilation with framework
  references removed and zero emitted assembly references.

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
not boot evidence.

```powershell
git diff --check
git status --short
```

`git diff --check` passed. The final pre-commit status contained only the eight
declared files above. Git emitted line-ending conversion warnings for four
existing tracked files; it reported no whitespace error.

An explicit first-party extension scan found no `.c`, `.cc`, `.cpp`, `.cxx`,
`.h`, `.hpp`, `.asm`, or `.s` file under `src`, `std`, or `apps`. A
changed-file credential-pattern scan also passed.

## Evidence

Final tool and source state:

- .NET SDK: `10.0.302`
- Git: `2.54.0.windows.1`
- Windows PowerShell: `5.1.26100.8894`
- Branch: `codex/M01-006-allocator-state-materializer`
- Base commit: `8de21398184a11e03f755e3696f9c66da434a5b8`

Final production, test, and documentation hashes:

- `BootPageAllocatorCoreContracts.cs`:
  `8332E013F343986D37F93036FEFE9EFFB1B9B8F596C34CF7CE2DF24E7A5F2D88`
- `BootPageAllocatorStateMaterializer.cs`:
  `C4F68B8175A9A8CFD9549434E869F45392474840A5B94ECF3E91DC2841E501C0`
- `BootPageAllocatorState.cs`:
  `4E7AFFDD9B1184A678F057D5E0EC5D8EE6868C819AC360C2DBA6269B32759A0C`
- `BootPageFreeListPlan.cs`:
  `856FF446A363885A77DAC5F8EC05AAA72F53480B8C7B0F0927940EA1911160D2`
- `BootPageAllocatorStateMaterializerTests.cs`:
  `1D4B6E223AB8C9A074EAE7599C18E81F6B1D351285027FE1639AE3A486B51EAD`
- `docs/boot.md`:
  `C22EC3845EF2E4EB889F37DBB37D8A63624A517E2B0F6E2C6B1679D390F5859C`
- `docs/architecture.md`:
  `DB88E11AD85DF28B62152B05562CFB8EB655E24E42C3DCFC955422F875FA535D`

The final unfiltered host stdout log completed at
`2026-07-26T05:49:56.7570459Z`:

- Development-host path:
  `C:\Users\Roger\AppData\Local\Temp\clros-m01-006-final-20260726T054542601Z.stdout.log`
- SHA-256:
  `53C5D597CE58948335F3381C98CF3E195667CF1049652959B1F734B01EB1F282`
- Bytes: `376546`
- Lines: `5894`
- Line-leading `^TEST_FAIL(?:\s|$)` terminal-marker count: `0`
- Exit marker: `CLROS_RUN_EXIT=0`

The separately redirected stderr file contained only the Windows PowerShell
module-initialization progress record and no test or compiler error.

The implementation was independently authored after reviewing this
owner-authorized reference snapshot path:

- `C:\Projects\CLROS\references\nativeaot-patcher-main\src\Cosmos.Kernel.Core\Memory\PageAllocator.cs`
  - SHA-256:
    `EC216B54C61765DC83F7CD6945614C2D97BFBDA6D2925668B12C4239D2E2368B`

The reference informed negative cases for missing maps, pointer/range bounds,
page arithmetic, alignment, list integrity, and end-exclusive containment.
CLROS does not copy its RAT/global-state, HHDM, Limine, exception, or allocator
architecture. Reference snapshots are read-only and worktree-isolated, so this
inspection used the canonical main-checkout path above rather than the
implementation worktree. The snapshot README hash recorded during review was
`D43494635178F9F1F5B54F990BF52580515479D56F561F129083133D7EEC1CA7`.

## Failures

- The first source-firewall test used a substring absence check for
  `BootPageAllocatorState`. That name is a prefix of the legitimate retained
  `BootPageAllocatorStateInitResult`, so the first full run reported
  `M01_006_SOURCE_FIREWALL_DIAGNOSTIC required-contract` even though the
  extraction was correct. The final test uses exact declaration regexes for
  both extracted PODs.
- Strict review found that the first pointer-span preflight used advertised
  structure-size constants rather than compiler `sizeof` values. The final
  implementation uses actual sizes for all pointer arithmetic and verifies
  advertised sizes independently.
- Strict review also found that a manually constructed result could claim
  success without revalidating capacity multiplication and page-byte
  arithmetic. The final `Succeeded` property delegates to a checked
  representability summary.
- A final hardening review observed that future layout drift could otherwise
  write a valid current state and return a nominal success whose `Succeeded`
  property was false. The final preflight rejects advertised/actual layout
  drift before traversal or the sole destination write.
- Concurrent development harness runs briefly held copied DLLs and caused one
  unrelated solution-build attempt to report `MSB3021`/`MSB3027`. Those
  overlapping runs were terminated and were not used as evidence. The final
  serialized solution build passed with 0 warnings and 0 errors.

## Deferred work

This step proves only host-tested materialization of allocator metadata from
caller-supplied typed nodes into caller-owned state.

It does not:

- Link the new leaf into `src/OS/OS.csproj` or invoke it from `EfiEntry`.
- Interpret the current six-`ulong` EFI raw node slots as the typed 48-byte
  node contract.
- Capture or mutate the live firmware memory map or the live EFI free-list
  metadata.
- Allocate, free, reserve, map, protect, or own any physical page.
- Install a live allocator or establish post-`ExitBootServices` ownership.
- Execute under QEMU/OVMF or provide guest, firmware, paging, or hardware
  evidence.
- Change `docs/completion-gates.md` or close any live boot-memory gate.

M01-007 owns the next host-only typed reservation-subtraction leaf. M01-023
later owns the checked pre-EBS EFI arena/transcode/wiring boundary, and
M01-024 owns its real-QEMU alloc/free evidence.

