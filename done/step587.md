# Step 587 - PAL memory allocation default result guard

## Summary

Tightened `OSHostMemoryAllocationResult.Succeeded` so a default/zero allocation
result no longer reports success. The guard preserves the existing 40-byte POD
layout by requiring a valid allocation handle and nonzero memory range instead
of adding fields.

## Why

PAL memory allocation is a hosted-runtime prerequisite. A zeroed allocation
result has `OSHostStatus.Ok` by default, so success must also require concrete
allocation evidence before future runtime, GC, or Kestrel work can trust this
mock boundary.

## Files changed

- `src/OS.Kernel/Pal/OSHostMemoryTable.cs`
- `tests/OS.HostTests/PalMemoryTableTests.cs`
- `docs/pal.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `work/PAL/pal-design.md`
- `done/step587.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'pal\.memory|TEST_PASS host\.tests|TEST_FAIL'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host build succeeded with 0 warnings and 0 errors.
- Filtered host run ended with `TEST_PASS host.tests`.
- Memory markers included:
  - `TEST_PASS pal.memory facade translates alloc free protect query`
  - `TEST_PASS pal.memory facade rejects null outputs and table`
  - `TEST_PASS pal.memory protect accepts write then execute`
  - `TEST_PASS pal.memory protect rejects rwx`
  - `TEST_PASS pal.memory protect rejects unaligned range`
  - `TEST_PASS pal.memory protect rejects overflow`
  - `TEST_PASS pal.memory protect rejects invalid transition`
  - `TEST_PASS pal.memory protect default result is not success`
  - `TEST_PASS pal.memory table allocates aligned pages`
  - `TEST_PASS pal.memory table rejects invalid request shape`
  - `TEST_PASS pal.memory table reports capacity exhaustion`
  - `TEST_PASS pal.memory table reuses freed ranges and rejects stale handles`
  - `TEST_PASS pal.memory table protects write then execute`
  - `TEST_PASS pal.memory table rejects stale and partial ranges`
  - `TEST_PASS pal.memory table layouts are stable`
  - `TEST_PASS pal.memory table default result is not success`
- Full solution build succeeded with 0 warnings and 0 errors.
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- `git diff --check` exited 0. It printed existing LF-to-CRLF working-copy
  warnings only.

## Failures

- No build, host-test, naming, source-firewall, or whitespace failures were
  observed for this slice.

## Deferred work

- This is host-tested in-process mock memory-table evidence only.
- No real page allocation, page-table mutation, TLB or instruction-cache work,
  GC allocation, hosted .NET memory behavior, Kestrel, cloud readiness, or
  production readiness is proven by this step.
