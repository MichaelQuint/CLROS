# Step 301 - Host-Only OSHost Wait/Event Facade

## What Changed

- Added `OSHostWaitFacade` as a host-only, future-export-shaped facade over `OSHostWaitTable`.
- Added `OSHostEventCreateOptions` as a POD event-create request shape for manual-reset and auto-reset events.
- Added status translation for event create/set/reset/close, immediate wait-one, immediate wait-many, invalid and stale handles, unsupported wait-all, unsupported non-immediate timeouts, null table handling, null output handling, handle-span metadata validation, and deterministic capacity exhaustion.
- Updated PAL and Kestrel docs to keep this boundary distinct from unmanaged `OSHost_*` exports, scheduler integration, hosted .NET, and Kestrel proof.

## Why It Changed

Issue #301 needed the wait/event PAL slice to match the existing socket, timer, and app-log facade pattern. Kestrel and hosted .NET will eventually need blocking waits, events, timers, ThreadPool, and synchronization behavior, but this step only proves deterministic host-side status translation over the current mock wait table.

## Files Changed

- `src/OS.Kernel/Pal/OSHostWaitFacade.cs`
- `tests/OS.HostTests/PalWaitFacadeTests.cs`
- `docs/pal.md`
- `work/PAL/pal-design.md`
- `docs/kestrel.md`
- `done/step301.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
git diff --check
```

## Logs And Evidence

- The first build attempt failed because a stale `OS.HostTests.exe` process held `tests/OS.HostTests/bin/Debug/net10.0/OS.Kernel.dll`. After waiting briefly and stopping that stale test process, the build was rerun.
- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors on rerun.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted `TEST_PASS host.tests`.
- `git diff --check` exited 0. It reported existing LF-to-CRLF warnings for many dirty working-tree files, but no whitespace errors.
- The host-test run emitted:

```text
TEST_PASS pal.wait facade creates manual and auto events
TEST_PASS pal.wait facade set reset close translate statuses
TEST_PASS pal.wait facade wait one reports signal and timeout
TEST_PASS pal.wait facade wait many reports first signaled
TEST_PASS pal.wait facade translates invalid and stale handles
TEST_PASS pal.wait facade rejects unsupported wait all and timeout
TEST_PASS pal.wait facade rejects null table
TEST_PASS pal.wait facade reports exhaustion
TEST_PASS pal.wait facade layouts are stable
```

## Failures

- Initial verification was blocked by the stale host-test process file lock described above. No issue-specific build or test failure remained after rerun.
- `git diff --check` printed line-ending normalization warnings only.

## Deferred Work

- No unmanaged `OSHost_WaitOne`, `OSHost_WaitMany`, `OSHost_EventCreate`, `OSHost_EventSet`, `OSHost_EventReset`, or `OSHost_EventClose` symbols are exported.
- No blocking waits, scheduler park/wake path, APIC/HPET/TSC timer integration, timer-completion wait handles, `Monitor`, ThreadPool, `Task`, hosted .NET 10, ASP.NET Minimal API, or Kestrel synchronization behavior is implemented.
- No guest or QEMU evidence is produced by this host-only facade.

## Evidence Boundary

This step proves host-only wait/event facade status translation over `OSHostWaitTable`. It does not prove a live PAL ABI, scheduler-backed blocking waits, runtime synchronization, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
