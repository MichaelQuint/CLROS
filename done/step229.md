# Step 229 - Profile-Aware Manifest Validation

## Summary

Made `OS.ManifestTool validate` profile-aware for the
`SingleMinimalApi` and `MultiMinimalApi` workload split.

## Why

Issue #250 needed the manifest validator to reject future multi-app manifest
shapes under the single-workload profile, and to return a precise unsupported
status for the multi-workload profile until process, isolation, and supervisor
semantics exist.

## Files changed

- `tools/OS.ManifestTool/OS.ManifestTool.csproj`
- `tools/OS.ManifestTool/Program.cs`
- `tests/OS.HostTests/ManifestToolTests.cs`
- `docs/workload-profiles.md`
- `docs/manifest.md`
- `docs/app-model.md`
- `docs/completion-gates.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet build .\OS.slnx --nologo -m:1 /p:OSWorkloadProfile=MultiMinimalApi`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'manifesttool|host\.tests'`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'build\.profile|manifesttool rejects default multi|host\.tests'`
- `.\scripts\check-naming.ps1`
- `.\scripts\run-qemu-smoke.ps1 -DryRun -RequireNativeAppMarkers`
- `git diff --check`

## Evidence

- Build passed with `Build succeeded.`, `0 Warning(s)`, and `0 Error(s)`.
- The full default-profile host-test run reached `TEST_PASS host.tests`.
- Manifest profile markers emitted:
  - `TEST_PASS manifesttool validates default single minimal api profile`
  - `TEST_PASS manifesttool rejects multiple apps under single minimal api`
  - `TEST_PASS manifesttool rejects multi app restart policy under single minimal api`
  - `TEST_PASS manifesttool rejects isolation resources under single minimal api`
  - `TEST_PASS manifesttool rejects multi minimal api until process model exists`
  - `TEST_PASS manifesttool rejects workload profile mismatch`
- The explicit `MultiMinimalApi` build passed with `Build succeeded.`,
  `0 Warning(s)`, and `0 Error(s)`.
- A no-build host-test run after the multi-profile build emitted
  `TEST_PASS manifesttool rejects default multi minimal api profile`,
  `TEST_PASS build.profile selected MultiMinimalApi`, and
  `TEST_PASS host.tests`.
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- `.\scripts\run-qemu-smoke.ps1 -DryRun -RequireNativeAppMarkers` emitted
  `TEST_PASS qemu.runner.dryrun`.
- `git diff --check` exited 0. It printed line-ending normalization warnings
  for modified Markdown and C# files only.

## Failures

An initial test pass failed three existing valid-manifest tests because overload
resolution treated the expected output string as the profile argument. The test
helper was split into an explicit `ExpectValidationForProfile` path and the
focused run then passed.

## Deferred work

- The validation is host-side only.
- Image-builder commands still do not require running `OS.ManifestTool`
  validation before image creation.
- The kernel-facing line manifest parser still does not consume workload
  profile metadata.
- No process table, per-app address space, supervisor, hosted .NET 10,
  ASP.NET Minimal API launch, or Kestrel behavior was implemented.
