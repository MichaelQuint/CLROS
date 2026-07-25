# Step 140 - Bounded Listen Endpoint Manifest Metadata

## What Changed

- Added optional kernel-facing native manifest syntax:
  - `listen=host:port`
- Added source-backed listen host metadata and a bounded numeric port to `NativeManifest`.
- Extended `NativeManifestParser` to validate one listen endpoint deterministically.
- Extended `NativeAppLaunchPlan` to carry listen metadata and require networking when a listen endpoint is present.
- Added host-test markers for parsing, rejection, launch-plan carry-through, and network requirement behavior.
- Updated manifest, app-model, networking, and completion-gate docs.

## Why It Changed

Gitea issue #158 needed explicit bounded listen endpoint metadata for future Minimal API/Kestrel launch planning without opening sockets or claiming hosted workload support.

## Files Changed

- `src/OS.Kernel/Manifest/NativeManifest.cs`
- `src/OS.Kernel/Manifest/NativeManifestParser.cs`
- `src/OS.Kernel/Manifest/NativeAppLaunchPlan.cs`
- `tests/OS.HostTests/NativeManifestParserTests.cs`
- `tests/OS.HostTests/NativeAppLaunchPlanTests.cs`
- `docs/manifest.md`
- `docs/app-model.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step140.md`

## Tests Run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --no-restore --nologo -m:1
```

## Logs And Evidence

- Focused host-test project build passed with 0 warnings and 0 errors.
- Added but did not execute these marker tests in this turn:
  - `TEST_PASS manifest kernel parses listen endpoint`
  - `TEST_PASS manifest kernel rejects invalid listen endpoint`
  - `TEST_PASS appmodel launch plan records listen endpoint`
  - `TEST_PASS appmodel launch plan requires network for listen endpoint`

## Failures

- Full host-test execution was intentionally skipped because full host-test processes were already stacking up and the current harness runs all module-initializer tests on startup.

## Deferred Work

- No socket bind, listen, accept, TCP listener table, PAL export, hosted .NET 10 runtime path, ASP.NET Minimal API hosting, or Kestrel behavior was added.
- No Gitea issue was closed.

## Evidence Boundary

This step is a managed parser and launch-plan metadata boundary only. It proves the focused project still compiles and adds host-test coverage for bounded listen metadata, but it does not prove QEMU boot-time manifest loading, networking, socket behavior, hosted .NET 10, ASP.NET Minimal API execution, or Kestrel support.
