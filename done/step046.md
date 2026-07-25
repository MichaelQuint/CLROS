# Step 046 - ExitBootServices Retry Plan Contract

## What Changed

- Added `UefiExitBootServicesPlan`, a host-tested plan-only contract for future firmware shutdown.
- Added `UefiExitBootServicesAttemptOutcome` and `UefiExitBootServicesPlanStatus`.
- Added `UefiExitBootServicesPlanResult` with status, handoff status, retry decision, attempt index, and retry limit.
- Modeled successful exit, fresh memory-map required, stale map-key retry, retry exhaustion, missing owned map, invalid descriptor metadata, and generic firmware exit failure.
- Added host tests for success, fresh-map requirement, stale-key retry, retry exhaustion, missing owned map, and invalid descriptor metadata.
- Updated boot, PAL, and completion-gate docs with the plan-only evidence boundary.

## Why It Changed

UEFI `ExitBootServices` can fail if the memory-map key is stale. The project needs a deterministic retry contract before adding any real firmware shutdown call, so the eventual implementation can be status-coded and testable instead of ad hoc.

## Files Changed

- `src/OS.Kernel/Boot/UefiExitBootServicesPlan.cs`
- `tests/OS.HostTests/UefiExitBootServicesPlanTests.cs`
- `docs/boot.md`
- `docs/pal.md`
- `docs/completion-gates.md`
- `done/step046.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

- Solution build passed with 0 warnings and 0 errors.
- Host test marker: `TEST_PASS uefi.exitboot plan reports success`
- Host test marker: `TEST_PASS uefi.exitboot plan requires fresh memory map`
- Host test marker: `TEST_PASS uefi.exitboot plan retries stale map key`
- Host test marker: `TEST_PASS uefi.exitboot plan reports retry exhaustion`
- Host test marker: `TEST_PASS uefi.exitboot plan rejects missing owned map`
- Host test marker: `TEST_PASS uefi.exitboot plan rejects invalid descriptor metadata`
- Full host test marker: `TEST_PASS host.tests`

## Failures

None.

## Deferred Work

- Wire a real firmware shutdown probe only after post-firmware diagnostics and owned output paths are ready.
- Preserve the fresh-map retry loop in the eventual no-stdlib EFI implementation.
- Ensure real `ExitBootServices` does not happen while any later code still depends on UEFI services.

## Evidence Boundary

This step is plan-only. It does not call `ExitBootServices`, does not mutate firmware state, does not prove post-firmware drivers, and does not claim hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
