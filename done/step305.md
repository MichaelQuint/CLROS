# Step 305 - Host-only PAL memory table and facade

## What changed

- Added `src/OS.Kernel/Pal/OSHostMemoryTable.cs`.
- Added `src/OS.Kernel/Pal/OSHostMemoryFacade.cs`.
- Added `tests/OS.HostTests/PalMemoryTableTests.cs`.
- Added `tests/OS.HostTests/PalMemoryFacadeTests.cs`.
- Updated `docs/pal.md`, `work/PAL/pal-design.md`, `docs/kestrel.md`, and `docs/completion-gates.md` with the issue #305 evidence boundary.

## Why it changed

Hosted .NET 10 and future Kestrel work need a deterministic PAL memory contract before exported `OSHost_*` symbols exist. This step models the status behavior of `OSHost_AllocPages`, `OSHost_FreePages`, `OSHost_ProtectPages`, and `OSHost_QueryMemory` in a host-only table/facade so later runtime agents can depend on stable page-size, alignment, handle, range, capacity, and W^X transition semantics.

## Implemented behavior

- `OSHostMemoryTable` validates page size, page alignment, page-count arithmetic, request alignment, finite mock capacity, and supported protection states.
- Allocations return generation-based opaque `OSHostHandle` values plus `OSHostMemoryRange` metadata.
- Freeing an allocation invalidates the handle and allows deterministic range reuse with a new generation.
- Query-by-address returns the active range, page size, and allocated state flags.
- Protection changes require an exact active range and route transition validation through `OSHostMemoryProtectionPlan`.
- `OSHostMemoryFacade` provides host-only exported-function-shaped allocation/free/protect/query status translation and validates caller-owned output pointers plus null table wiring.

## Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` passed with `TEST_PASS host.tests`.
- `git diff --check` exited 0. It reported only existing LF-to-CRLF working-copy warnings.

Observed new host markers:

```text
TEST_PASS pal.memory table allocates aligned pages
TEST_PASS pal.memory table rejects invalid request shape
TEST_PASS pal.memory table reports capacity exhaustion
TEST_PASS pal.memory table reuses freed ranges and rejects stale handles
TEST_PASS pal.memory table protects write then execute
TEST_PASS pal.memory table rejects stale and partial ranges
TEST_PASS pal.memory table layouts are stable
TEST_PASS pal.memory facade translates alloc free protect query
TEST_PASS pal.memory facade rejects null outputs and table
```

## Deferred work

- No unmanaged `OSHost_*` memory symbols are exported.
- No real pages are allocated or freed.
- No page tables, TLB state, instruction caches, NX/W^X runtime enforcement, GC heap, hosted runtime, or Kestrel path is touched.
- The current host model requires exact active ranges for protection changes; subrange protection can be added later when a real page table/range-split model exists.
