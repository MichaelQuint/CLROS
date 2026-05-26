# Step 120 - Manifest Package Runtime Selection Fields

## What Changed

- Added kernel-facing native manifest selection fields for `app.kind=package` and `runtime.kind=native-aot`.
- Kept older v0 native manifests compatible by defaulting omitted selection fields to packaged native AOT.
- Added status-coded parser rejection for unsupported runtime selections such as `runtime.kind=dotnet10`.
- Extended `NativeAppLaunchPlan` to preserve and validate packaged native app/runtime selection without launching app code.
- Updated manifest, app-model, Kestrel, and completion-gate docs to keep the hosted .NET/Kestrel evidence boundary explicit.

## Why It Changed

Issue #136 needed the manifest/app-model contract to describe the selected packaged native app and reserve hosted runtime values before any app launcher, hosted .NET runtime, or Kestrel path exists.

## Files Changed

- `src/OS.Kernel/Manifest/NativeManifest.cs`
- `src/OS.Kernel/Manifest/NativeManifestParser.cs`
- `src/OS.Kernel/Manifest/NativeAppLaunchPlan.cs`
- `tests/OS.HostTests/NativeManifestParserTests.cs`
- `tests/OS.HostTests/NativeManifestLoaderTests.cs`
- `tests/OS.HostTests/NativeAppLaunchPlanTests.cs`
- `docs/manifest.md`
- `docs/app-model.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Solution build passed with 0 warnings and 0 errors.
- Parent-owned full host tests emitted:
  - `TEST_PASS manifest kernel parses package app fields`
  - `TEST_PASS manifest kernel rejects unsupported runtime kind`
  - `TEST_PASS appmodel launch plan accepts packaged native app`
  - `TEST_PASS host.tests`

## Failures

- None in the parent-owned verification run.

## Deferred Work

- No app launcher, app ABI, binary mapping, hosted .NET 10 runtime, ASP.NET Minimal API launch, or Kestrel socket binding was added.
- Reserved hosted runtime selections remain unsupported in the kernel-facing manifest parser.

## Evidence Boundary

This step proves manifest/app selection contract behavior only. It does not prove package loading in the live boot path, native app execution, hosted .NET 10, ASP.NET Minimal API, or Kestrel.
