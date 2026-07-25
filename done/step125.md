# Step 125 - PAL Config And Environment Query Table

## What Changed

- Added `OSHostEnvironmentEnumerationQuery` as a POD ABI declaration for future environment enumeration calls.
- Added `OSHostConfigEnvironmentTable` as a host-tested mock table for manifest config and environment values.
- Added status-coded query results that copy values into caller-owned buffers and report required byte counts for too-small destinations.
- Added deterministic environment enumeration by index with bounded key/value copies.
- Added host tests for config lookup, buffer-too-small handling, environment lookup, environment enumeration, and invalid query handling.
- Updated PAL, PAL design, Kestrel, and completion-gate docs.

## Why It Changed

Issue #141 needed a PAL-facing contract for manifest config and environment access before exported `OSHost_*` functions, hosted .NET 10, or ASP.NET/Kestrel configuration propagation exist. This keeps the boundary status-coded and caller-buffer based.

## Files Changed

- `src/OS.Kernel/Pal/OSHostAbi.cs`
- `src/OS.Kernel/Pal/OSHostConfigEnvironmentTable.cs`
- `tests/OS.HostTests/PalAbiLayoutTests.cs`
- `tests/OS.HostTests/PalConfigEnvironmentTests.cs`
- `docs/pal.md`
- `work/PAL/pal-design.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `done/step125.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
.\scripts\run-qemu-smoke.ps1 -DryRun
git diff --check
```

## Logs And Evidence

- Solution build passed with 0 warnings and 0 errors.
- Parent-owned full host tests emitted:
  - `TEST_PASS pal.config returns manifest config value`
  - `TEST_PASS pal.config reports buffer too small`
  - `TEST_PASS pal.env returns manifest environment value`
  - `TEST_PASS pal.env enumerates environment entries`
  - `TEST_PASS pal.env rejects invalid queries`
  - `TEST_PASS host.tests`
- Naming/source guard emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- QEMU dry-run emitted `TEST_PASS qemu.runner.dryrun`.
- `git diff --check` exited successfully. It still printed existing line-ending normalization warnings.

## Failures

- An intermediate host-test run failed while issue #143 was in progress because descriptor behavior had changed before its tests were updated. The final parent-owned verification reran after integration and passed.

## Deferred Work

- No exported `OSHost_GetConfigValue`, `OSHost_EnumerateConfig`, `OSHost_GetEnvironmentValue`, or `OSHost_EnumerateEnvironment` symbols were added.
- No hosted .NET 10 runtime, ASP.NET configuration, appsettings binding, process environment propagation, or Kestrel behavior was added.

## Evidence Boundary

This step proves a host-tested PAL config/environment mock contract only. It does not prove exported PAL functions, guest config reads, hosted runtime configuration, ASP.NET Minimal API, or Kestrel.
