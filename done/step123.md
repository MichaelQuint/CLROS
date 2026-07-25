# Step 123 - Kernel Manifest Environment Fields

## What Changed

- Added fixed-size, source-backed environment entries to the kernel-facing native manifest model.
- Extended `NativeManifestParser` to accept `env.KEY=value` fields with deterministic limits.
- Added parser statuses for duplicate, invalid, too many, and oversized environment fields.
- Extended `NativeAppLaunchPlan` to carry manifest environment entries as metadata.
- Added host tests for parsing environment fields, rejecting duplicate keys, rejecting invalid keys, rejecting too many entries, and carrying environment fields into the launch plan.
- Updated manifest, app-model, Kestrel, and completion-gate docs.

## Why It Changed

Issue #140 needed manifest-provided environment metadata before PAL config/environment query work and future ASP.NET Minimal API configuration can be represented. This keeps the data source-backed and status-coded without implementing app launch or hosted runtime propagation.

## Files Changed

- `src/OS.Kernel/Manifest/NativeManifest.cs`
- `src/OS.Kernel/Manifest/NativeManifestParser.cs`
- `src/OS.Kernel/Manifest/NativeAppLaunchPlan.cs`
- `tests/OS.HostTests/NativeManifestParserTests.cs`
- `tests/OS.HostTests/NativeAppLaunchPlanTests.cs`
- `docs/manifest.md`
- `docs/app-model.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `done/step123.md`

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
  - `TEST_PASS manifest kernel parses environment fields`
  - `TEST_PASS manifest kernel rejects duplicate environment keys`
  - `TEST_PASS manifest kernel rejects invalid environment key`
  - `TEST_PASS manifest kernel rejects too many environment fields`
  - `TEST_PASS appmodel launch plan carries environment fields`
  - `TEST_PASS host.tests`
- Naming/source guard emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- QEMU dry-run emitted `TEST_PASS qemu.runner.dryrun`.
- `git diff --check` exited successfully. It still printed existing line-ending normalization warnings.

## Failures

- None in the parent-owned verification run.

## Deferred Work

- PAL config/environment query functions are not implemented.
- Environment fields are not process environment variables and are not exposed to hosted .NET or ASP.NET configuration.
- No app launcher, app ABI, hosted .NET 10 runtime, ASP.NET Minimal API launch, or Kestrel socket binding was added.

## Evidence Boundary

This step proves host-tested kernel manifest environment metadata parsing and launch-plan carry-through only. It does not prove PAL environment queries, guest app launch, hosted .NET 10, ASP.NET configuration, or Kestrel.
