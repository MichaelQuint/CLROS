# Step 314 - QEMU HTTP Probe Serial Sharing Fix

## What Changed

- Changed the fake-QEMU HTTP probe fixture to write `TEST_START fake.qemu` and `OS_SMOKE_PASS` through one .NET `FileStream` opened with `FileShare.ReadWrite | FileShare.Delete`.
- Removed the `Set-Content` plus `Add-Content` sequence that could reopen the serial log while `OS.QemuRunner` was polling it on Windows.
- Updated QEMU documentation with the serial-log sharing contract and host-test evidence.

## Why It Changed

Issue #314 tracked a Windows file sharing race in the fake-QEMU HTTP probe tests. The runner already reads the serial log with permissive sharing, but the fake PowerShell guest reopened the file for append immediately after creating it. During host-test polling, that second open could fail with an `IOException`, causing the test to report a runner timeout instead of the intended HTTP probe result.

## Files Changed

- `tests/OS.HostTests/QemuRunnerHttpProbeTests.cs`
- `docs/qemu.md`
- `done/step314.md`

## Evidence

Focused host-test project compile:

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo --no-dependencies
```

Result: passed with 0 warnings and 0 errors.

Full host-test run:

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

Result: emitted `TEST_PASS host.tests`.

Focused markers observed:

```text
TEST_PASS qemu.runner.http_probe accepts expected response
TEST_PASS qemu.runner.http_probe rejects status mismatch
TEST_PASS qemu.runner.http_probe rejects body mismatch
TEST_PASS qemu.runner.http_probe times out deterministically
```

## Deferred Work

- This does not add live guest HTTP probing.
- This does not prove Kestrel or Minimal API traffic inside the OS.
- The HTTP probe remains host-runner infrastructure until guest networking and hosted .NET are wired into real QEMU smoke.

## Evidence Boundary

This step proves deterministic fake-QEMU serial marker writes while the runner polls the serial log. It does not change real QEMU launch semantics or weaken marker checks.
