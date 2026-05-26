# Step 127 - Manifest To PAL Config Bridge

## What Changed

- Added a host-tested builder that creates `OSHostConfigEnvironmentTable` from manifest source bytes and a successful `NativeAppLaunchPlan`.
- Exposed deterministic config keys for `app.name`, `app.path`, `app.args`, `app.kind`, `runtime.kind`, `network`, and `log`.
- Exposed manifest `env.KEY=value` entries through the PAL environment table.
- Added deterministic invalid-slice failure handling for source-backed manifest fields.
- Added host tests for table construction, launch-field lookup, environment lookup, and invalid source slices.
- Updated PAL, Kestrel, and completion-gate docs.

## Why It Changed

Issue #146 bridges the kernel native manifest metadata and the PAL config/environment mock table. This gives later hosted-runtime work a tested path from manifest app selection to config/environment values without exporting PAL symbols or claiming hosted .NET behavior.

## Files Changed

- `src/OS.Kernel/Pal/OSHostConfigEnvironmentTable.cs`
- `tests/OS.HostTests/PalConfigEnvironmentTests.cs`
- `docs/pal.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `done/step127.md`

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
  - `TEST_PASS pal.config table builds from native manifest`
  - `TEST_PASS pal.config table exposes launch fields`
  - `TEST_PASS pal.env table exposes manifest environment`
  - `TEST_PASS pal.config table rejects invalid manifest slices`
  - `TEST_PASS host.tests`
- Naming/source guard emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- QEMU dry-run emitted `TEST_PASS qemu.runner.dryrun`.
- `git diff --check` exited successfully. It still printed existing line-ending normalization warnings.

## Failures

- None in the parent-owned verification run.

## Deferred Work

- No exported `OSHost_*` config/environment functions were added.
- No guest config reads, hosted .NET 10 runtime, ASP.NET configuration binding, process environment propagation, or Kestrel behavior was added.

## Evidence Boundary

This step proves host-tested manifest-to-PAL config/environment mock bridging only. It does not prove exported PAL behavior, guest config reads, hosted runtime configuration, ASP.NET Minimal API, or Kestrel.
