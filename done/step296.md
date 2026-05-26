# Step 296 - Allocator-Facing Boot Memory-Region Selection Plan

## What Changed

- Added `BootMemoryRegionPlanner` as the host-tested planning boundary after compact boot memory-region materialization.
- Added caller-owned output records for conventional-memory usable ranges and reserved/unusable range metadata.
- Added status-coded validation for empty source input, zero page counts, page byte-count overflow, physical-address overflow, byte-length mismatch, physical-end mismatch, unsorted ranges, overlapping ranges, and destination capacity.
- Added host tests for conventional range selection, reserved metadata recording, overlap rejection, usable/reserved destination-capacity reporting, invalid metadata, and overflow failures.
- Updated boot, architecture, and completion-gate docs to identify this as host-tested planning only.

## Why It Changed

Issue #296 needed a narrow allocator-facing contract that consumes the compact `BootMemoryRegionRecord` values produced by the earlier boot materializer and reduces them into the ranges a later page allocator can consume. This keeps the boundary deterministic and allocation-free without implementing allocator ownership, free lists, paging, direct maps, NX/W^X, or `ExitBootServices`.

## Files Changed

- `src/OS.Kernel/Memory/BootMemoryRegionPlan.cs`
- `tests/OS.HostTests/BootMemoryRegionPlannerTests.cs`
- `docs/boot.md`
- `docs/architecture.md`
- `docs/completion-gates.md`
- `done/step296.md`

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
dotnet build .\OS.slnx --nologo -m:1
git diff --check
```

## Logs And Evidence

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `git diff --check` returned exit code 0. It reported existing LF-to-CRLF warnings for many dirty working-tree files, but no whitespace errors.
- The final `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` run emitted `TEST_PASS host.tests`.
- The host-test run emitted the new planner markers:

```text
TEST_PASS memory.boot regions selects conventional ranges
TEST_PASS memory.boot regions records reserved ranges
TEST_PASS memory.boot regions rejects overlapping ranges
TEST_PASS memory.boot regions reports destination capacity
TEST_PASS memory.boot regions rejects invalid metadata
TEST_PASS memory.boot regions covers overflow failures
```

## Failures

- The first full host-test attempt exited 1 after the new planner tests passed. The later unrelated `qemu.runner.http_probe rejects body mismatch` test failed because the fake QEMU script could not append to its temp serial log while another process held the file:

```text
Add-Content : The process cannot access the file ... http-probe-...-serial.log because it is being used by another process.
TEST_FAIL qemu.runner.http_probe rejects body mismatch
```

- A second host-test run passed and emitted `TEST_PASS host.tests`.

## Deferred Work

- No page allocator, free list, paging structure, direct map, NX/W^X policy, or executable memory policy was implemented.
- The planner does not reserve kernel image, boot stack, descriptor storage, ACPI, framebuffer, or future driver-owned ranges beyond recording non-conventional metadata.
- The live EFI path still materializes compact `BootMemoryRegionRecord` entries only; it does not run `BootMemoryRegionPlanner` or emit a new QEMU marker for this issue.
- No `ExitBootServices` behavior is changed.

## Evidence Boundary

This step proves the host-tested planner can validate compact boot memory-region records, select conventional ranges as allocator-facing candidates, record reserved/unusable metadata, report caller-owned destination capacity, and reject overlap/metadata/overflow failures. It does not prove allocator ownership, live EFI integration, post-firmware execution, page allocation, paging, driver ownership, storage, networking, hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior.
