# Step 228 - Workload Profile Build Selector

## Summary

Added the initial `OSWorkloadProfile` build selector and a kernel-facing
profile metadata contract for `SingleMinimalApi` and `MultiMinimalApi`.

## Why

Issue #249 needed a small, verifiable way to distinguish the near-term
single-Minimal-API CloudReady path from the later multi-Minimal-API path without
claiming process, supervisor, hosted .NET, or Kestrel runtime behavior.

## Files changed

- `Directory.Build.props`
- `Directory.Build.targets`
- `src/OS.Kernel/Build/WorkloadProfile.cs`
- `tests/OS.HostTests/WorkloadProfileTests.cs`
- `docs/workload-profiles.md`
- `docs/app-model.md`
- `docs/manifest.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet build .\OS.slnx --nologo -m:1 /p:OSWorkloadProfile=MultiMinimalApi`
- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1 /p:OSWorkloadProfile=BadProfile`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Default `dotnet build .\OS.slnx --nologo -m:1` passed with
  `Build succeeded.`, `0 Warning(s)`, and `0 Error(s)`.
- Default host tests passed and emitted:
  - `TEST_PASS build.profile selected SingleMinimalApi`
  - `TEST_PASS build.profile selects exactly one profile`
  - `TEST_PASS build.profile records capability requirements`
  - `TEST_PASS host.tests`
- Explicit `dotnet build .\OS.slnx --nologo -m:1 /p:OSWorkloadProfile=MultiMinimalApi`
  passed with `Build succeeded.`, `0 Warning(s)`, and `0 Error(s)`.
- A no-build host-test run after the multi-profile build emitted:
  - `TEST_PASS build.profile selected MultiMinimalApi`
  - `TEST_PASS build.profile selects exactly one profile`
  - `TEST_PASS build.profile records capability requirements`
  - `TEST_PASS host.tests`
- Invalid profile verification failed deterministically with
  `Invalid OSWorkloadProfile 'BadProfile'. Valid values: SingleMinimalApi, MultiMinimalApi.`
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- `git diff --check` exited 0. It printed line-ending normalization warnings
  for modified Markdown files only.
- `.\scripts\run-qemu-smoke.ps1 -DryRun -RequireNativeAppMarkers` emitted
  `TEST_PASS qemu.runner.dryrun`.

## Failures

None.

## Deferred work

- The selector only emits compile symbols and profile metadata.
- It does not implement app execution, a process table, a multi-app manifest,
  a supervisor, hosted .NET 10, ASP.NET Minimal API launch, or Kestrel.
- Manifest validation for profile-specific multi-app rejection remains a
  separate issue.
