# Step 856 - M01-009 receipt-bound one-page exact-inverse free

## Summary

Completed Gitea issue `M01-009` / issue `#33` on branch
`codex/M01-009-typed-free-coalescing`.

`BootPageAllocatorMutator.FreeOnePage` is now the host-tested,
no-stdlib-compatible exact inverse of one successful real
`AllocateOnePage` transition. The free boundary accepts the complete typed
allocation result and its embedded restore receipt, not an independently
selected raw address. It validates the exact current state, storage
counts/capacities, reservations, active free-list chain, allocation absence,
receipt geometry, predecessor/successor topology, and expected post-allocation
node shapes before changing caller-owned metadata.

Successful restoration returns the original allocator state and reports one
of four deterministic classifications:

- `None` for an exactly consumed node.
- `Previous` for a tail allocation.
- `Next` for a head allocation.
- `PreviousAndNext` for a middle split.

The original source-node and predecessor POD values are restored, including
their raw padding bytes. Middle allocation may claim only a completely zero
48-byte inactive slot, and exact-inverse free clears only that inserted slot.
The existing `BootPageAllocatorMutationStatus` values remain exactly `0`–`39`.
The retained internal synthetic-success factory remains successful but
non-restorable, preserving the M01-008 compatibility boundary.

## Why

M01-008 crossed only the caller-owned one-page allocation metadata boundary.
The next dependency required a typed, failure-atomic way to undo that exact
transition, reject replay/double free and stale or out-of-order state, and
preserve original node provenance/topology without claiming general
arbitrary-address freeing or live physical-page ownership.

The restore receipt is intentionally copyable evidence rather than an
unforgeable capability. The implemented contract supports immediate or
caller-serialized LIFO restoration. It has no allocator identity or generation,
cannot distinguish byte-identical allocator copies, and cannot eliminate ABA
after free and reallocation.

## Files changed

- `src/OS.Kernel/Memory/BootPageAllocatorMutation.cs`
  - Added `BootPageAllocatorOnePageRestoreReceipt`.
  - Added `BootPageAllocatorOnePageCoalescingKind`.
  - Extended real allocation results with `RestoreReceipt` and `CanRestore`.
  - Added `BootPageAllocatorOnePageFreeResult`.
  - Added `BootPageAllocatorMutator.FreeOnePage`.
  - Added full 48-byte zero-slot validation and restored-successor ordering.
- `tests/OS.HostTests/BootPageAllocatorFreeCoalescingTests.cs`
  - Added 23 self-registering M01-009 host-test groups.
- `docs/boot.md`
  - Recorded the exact inverse, failure matrix, no-stdlib closure, references,
    markers, limitations, and host-only evidence boundary.
- `docs/architecture.md`
  - Recorded the typed receipt/free surface and identity-free/LIFO boundary.
- `done/step856.md`
  - Preserved this implementation and verification evidence.

## Tests run

All commands ran from `C:\Projects\CLROS-M01-009`.

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\CLROS.slnx --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun
git diff --check
```

Results:

- Kernel build: passed with zero warnings and zero errors.
- `CLROS.slnx` build: passed with zero warnings and zero errors.
- Host-test build: passed with zero warnings and zero errors.
- Full host harness: exit code `0`; final marker `TEST_PASS host.tests`.
- Naming guard: `TEST_PASS naming.guard`.
- Source firewall: `TEST_PASS source.firewall`.
- QEMU runner dry-run: `TEST_PASS qemu.runner.dryrun`.
- `git diff --check`: passed.

## Evidence

The authoritative full host-harness stdout is:

`C:\Users\Roger\AppData\Local\Temp\clros-m01-009-authoritative-20260726T081946296Z.stdout.log`

- Byte length: `772296`
- Line count: `6048`
- SHA-256:
  `C90F8C4EC05F0713E7A7621AE43C53D29AD96D8D5EB7D463C0E8D5FA0862D71A`
- Line-leading `TEST_FAIL` count: `0`

The authoritative stderr is:

`C:\Users\Roger\AppData\Local\Temp\clros-m01-009-authoritative-20260726T081946296Z.stderr.log`

- Byte length: `0`
- Line count: `0`
- SHA-256:
  `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`

The issue-level markers each occurred exactly once and in order:

```text
TEST_START m01.009
CLROS_M01_009_READY
TEST_PASS m01.009
```

The complete harness ended with exactly one:

```text
TEST_PASS host.tests
```

The 23 groups cover:

- Exact enum ordinals, API shape, unmanaged sequential readonly results, and
  default-failing values.
- Whole-head, whole-nonhead, head, tail, and middle exact inverses.
- Exact state/accounting reversal, source provenance, predecessor restoration,
  reservation preservation, logical-count preservation, and capacity canaries.
- Strict LIFO, replay, double-free, out-of-order, stale, altered,
  nonmatching-foreign, and non-restorable evidence.
- Altered predecessor index, geometry, provenance, and link.
- Default/zero, unaligned, and near-`ulong`-maximum wrapping typed geometry.
- Source/successor order reversal and already-free/reserved overlap.
- Corrupt state, indexes, chains, cycles, totals, capacities, table aliases,
  arithmetic overflow, and byte-for-byte failure atomicity.
- Nonzero inactive-slot padding rejection and active-node raw-padding
  round-trip.
- Allocation-free deterministic execution.
- Twelve bounded hostile-pointer subprocess cases.
- Evaluated Release EFI source/reference exclusion.
- Exact `NOSTDLIB_EFI` compile closure with zero assembly references.
- Host-only marker uniqueness and ordering.

Tool versions:

- .NET SDK: `10.0.302` (`win-x64`, commit `35b593bebf`)
- Git: `2.54.0.windows.1`
- Windows PowerShell: `5.1.26100.8894`

Reference use was behavioral/test-only; no implementation was copied:

- `references/abanu-master/src/Abanu.Kernel.Core/MemoryManagement/PageAllocators/BuddyAllocatorImplementation.cs`
  - SHA-256:
    `11913EDC30E2E4210A4F69BD352F9389CB7BEECAEF108CCF9E9A67272E6F4A3E`
- `references/nativeaot-patcher-main/src/Cosmos.Kernel.Core/Memory/PageAllocator.cs`
  - SHA-256:
    `EC216B54C61765DC83F7CD6945614C2D97BFBDA6D2925668B12C4239D2E2368B`

## Failures

Independent review found and corrected two production defects before the
authoritative run:

- A coherent forged whole-node receipt could place the restored source after
  its recorded successor and return success with an unsorted chain. Final
  topology validation now requires an active successor and
  `source.EndExclusive <= successor.Start`.
- An inactive slot with zero-valued fields but nonzero trailing padding could
  be claimed for a split. Selection and whole-node absence validation now
  inspect all 48 raw node bytes.

Final review also found missing zero/wrap/predecessor negative vectors and an
overbroad generic foreign-receipt documentation claim. The tests now cover
those vectors, and the documentation explicitly limits rejection to foreign
evidence that differs; indistinguishable allocator copies remain a non-claim.

One preliminary full-harness run reached `TEST_PASS host.tests`, but its
PowerShell `Start-Process -Wait` wrapper remained alive after the `dotnet`
child exited. That obsolete wrapper was terminated after confirming the child
was gone. The separate authoritative run above used direct invocation, wrote
exit code `0`, and exited normally.

No unresolved implementation or test failure remains in this issue.

## Deferred work

This is host-only metadata evidence. It does **not**:

- Free, own, reserve, zero, map, protect, or access a live physical page.
- Provide arbitrary-order free, an allocation ledger, allocator identity,
  generation-bearing authority, unforgeable receipts, or ABA protection.
- Link the typed allocate/free source into `src/OS/OS.csproj`.
- Invoke the boundary from `EfiEntry`.
- Provide QEMU/OVMF guest execution evidence.
- Change `docs/completion-gates.md` or close any live boot/memory gate.

M01-024 retains ownership of the real pre-`ExitBootServices` guest
allocation/free/double-free-rejection cycle. M01-010 is the next dependency
after tracker synchronization makes it ready.
