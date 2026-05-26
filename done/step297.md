# Step 297 - Host-Only OSHost Timer Facade

## What Changed

- Added `OSHostTimerFacade` as a host-only, future-export-shaped facade over `OSHostTimerTable`.
- Added status translation for monotonic snapshots, one-shot timer create/arm, cancel/close, invalid handles, table exhaustion, null table handling, and poll output layout.
- Updated PAL/Kestrel docs to keep this boundary distinct from unmanaged `OSHost_*` exports, scheduler integration, and hardware timers.

## Why It Changed

Issue #297 needed the timer PAL to match the socket and app-log pattern: a deterministic C# facade that models the future `OSHost_*` call shape without exporting unmanaged symbols or claiming hosted .NET behavior. Kestrel will eventually need monotonic timeouts and timer primitives, but this step only proves status translation over a mock table.

## Files Changed

- `src/OS.Kernel/Pal/OSHostTimerFacade.cs`
- `tests/OS.HostTests/PalTimerFacadeTests.cs`
- `docs/pal.md`
- `work/PAL/pal-design.md`
- `docs/kestrel.md`
- `done/step297.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
git diff --check
```

## Logs And Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted `TEST_PASS host.tests`.
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited 0. It reported existing LF-to-CRLF warnings for many dirty working-tree files, but no whitespace errors.
- The host-test run emitted:

```text
TEST_PASS pal.timer facade returns monotonic snapshot
TEST_PASS pal.timer facade creates and arms one shot
TEST_PASS pal.timer facade cancels and closes handles
TEST_PASS pal.timer facade translates invalid handles
TEST_PASS pal.timer facade reports exhaustion
TEST_PASS pal.timer facade rejects null table
TEST_PASS pal.timer facade poll output layout is stable
```

## Failures

- No issue-specific failures remained after integration. `git diff --check` printed line-ending normalization warnings only.

## Deferred Work

- No unmanaged `OSHost_*` symbols are exported.
- No APIC, HPET, TSC, scheduler, wait integration, ThreadPool, `Task.Delay`, hosted .NET, ASP.NET Minimal API, or Kestrel behavior is implemented.
- No guest or QEMU evidence is produced by this host-only facade.

## Evidence Boundary

This step proves host-only timer facade status translation over `OSHostTimerTable`. It does not prove a live PAL ABI, hardware timer source, scheduler wakeups, managed runtime timers, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
