# Step 130 - Kestrel Minimal API Smoke Package Contract

## What Changed

- Added the `kestrel.smoke.package.v0` contract to `docs/kestrel.md`.
- Defined the first in-guest ASP.NET Minimal API smoke fixture details: route paths, deterministic response body, required manifest environment/config key, package-relative file layout, listen endpoint, manifest fields, and serial/runner markers.
- Cross-referenced the contract from app-model, networking, PAL, and completion-gate docs without claiming Kestrel support.

## Why It Changed

Gitea issue #149 needed a narrow ledger for the first in-guest Minimal API/Kestrel smoke package so future work cannot substitute host-only Minimal API builds, dry-run HTTP probe text, native-tier HTTP demos, or mock PAL tests for Kestrel evidence.

## Files Changed

- `docs/kestrel.md`
- `docs/app-model.md`
- `docs/networking.md`
- `docs/pal.md`
- `docs/completion-gates.md`
- `done/step130.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
.\scripts\run-qemu-smoke.ps1 -DryRun
rg -n "Gitea issue #149|kestrel\.smoke\.package\.v0|OS_MINIMAL_API_OK|OS_SMOKE_EXPECTED_BODY|TEST_START kestrel\.smoke|TEST_PASS kestrel\.smoke|HTTP_PROBE_PASS" .\docs\kestrel.md .\docs\app-model.md .\docs\networking.md .\docs\pal.md .\docs\completion-gates.md
git diff --check
```

## Logs And Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted:
  - `TEST_PASS manifesttool validates reserved dotnet10 kestrel contract`
  - `TEST_PASS qemu dry-run emits HTTP probe contract`
  - `TEST_PASS qemu real mode rejects HTTP probe execution`
  - `TEST_PASS host.tests`
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `.\scripts\run-qemu-smoke.ps1 -DryRun` emitted `TEST_PASS qemu.runner.dryrun`.
- The documentation check found `kestrel.smoke.package.v0`, `OS_MINIMAL_API_OK`, `OS_SMOKE_EXPECTED_BODY`, `TEST_START kestrel.smoke`, `TEST_PASS kestrel.smoke`, and `HTTP_PROBE_PASS`.
- `git diff --check` exited successfully. It printed existing line-ending normalization warnings for the shared dirty tree.

## Failures

- None in this verification run.

## Deferred Work

- No hosted .NET runtime, exported PAL functions, package builder integration, QEMU HTTP probe implementation, guest TCP listener, scheduler/threading support, or Kestrel implementation was added.

## Evidence Boundary

This step proves documentation/test-contract coverage only. It does not prove package production, in-guest app launch, hosted .NET 10 startup, PAL file/config propagation, PAL sockets, TCP listener behavior, runner HTTP verification, ASP.NET Minimal API execution inside OS, or Kestrel support.
