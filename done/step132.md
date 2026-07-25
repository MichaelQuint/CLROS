# Step 132 - Native Payload Mapping Plan

## What Changed

- Added `NativeAppPayloadMappingPlanner` and status/result contracts.
- The planner consumes a successful selected native app descriptor and computes deterministic payload, header, executable-content, and entrypoint ranges for a future executable-mapping step.
- Added host tests for valid range planning, descriptor failure propagation, zero executable payload rejection, and entry offsets outside the selected payload.
- Updated app-model, completion-gate, NativeAOT/no-stdlib, and security docs with the evidence boundary.

## Why It Changed

Gitea issue #150 needed the next narrow planning boundary after `OSAPPv0` payload validation. The OS can now describe what bytes would be mapped and where the entrypoint sits before adding real page allocation, page permissions, relocation, or app invocation.

## Files Changed

- `src/OS.Kernel/Runtime/NativeAppPayloadMappingPlan.cs`
- `tests/OS.HostTests/NativeAppPayloadMappingPlanTests.cs`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/security.md`
- `done/step132.md`

## Tests Run

Pending integrated parent verification:

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
.\scripts\run-qemu-smoke.ps1 -DryRun
git diff --check
```

## Logs And Evidence

Expected new markers:

- `TEST_PASS appmodel payload mapping plans executable range`
- `TEST_PASS appmodel payload mapping rejects descriptor failure`
- `TEST_PASS appmodel payload mapping rejects zero executable bytes`
- `TEST_PASS appmodel payload mapping rejects invalid entry range`

## Failures

- None known before integrated verification.

## Deferred Work

- No executable memory allocation was added.
- No page protection, W^X, or NX transition was added.
- No relocation or complete executable-format validation was added.
- No native app entrypoint invocation was added.
- No hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior was added.

## Evidence Boundary

This step is host-tested range planning only. It does not prove live boot package loading, complete executable validation, executable mapping, native-tier app execution, hosted .NET 10 startup, ASP.NET Minimal API execution, or Kestrel support.
