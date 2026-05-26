# Step 291 - ExitBootServices Result Boundary

## What Changed

- Added a platform-level `ExitBootServices` result contract around the firmware shutdown call.
- Introduced `PlatformFirmwareExitBootServicesResult` and `PlatformFirmwareExitBootServicesStatus` so firmware outcomes are explicit instead of being treated as raw status values at higher boot layers.
- Mapped UEFI success to success, UEFI invalid-parameter/map-key rejection to a stale map-key result, unavailable function pointers to a controlled unavailable-services result, and all other firmware statuses to firmware-error results.
- Added `UefiExitBootServicesAttemptMapper` to translate platform firmware results into the boot planner's `UefiExitBootServicesAttemptOutcome` values.
- Kept the contract status-coded. No exceptions, managed references, or firmware-specific objects cross this boundary.
- Updated the related host-test plan so the boundary can be verified without performing a real firmware shutdown.

## Why It Changed

`ExitBootServices` is the transition from firmware-owned boot services to OS-owned
drivers and memory management. The OS cannot make that transition safely while
call results are unstructured. This slice gives the boot sequence a small,
testable boundary for handling the two most important early outcomes: a successful
firmware exit and a rejected/stale memory-map key that requires recapturing the
memory map before retrying.

## Files Changed

- `src/OS.Kernel/Platform/IPlatformFirmware.cs`
- `src/OS.Kernel/Boot/UefiExitBootServicesAttemptMapper.cs`
- `tests/OS.HostTests/UefiExitBootServicesPlanTests.cs`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/efi-build.md`
- `docs/completion-gates.md`
- `docs/pal.md`
- `done/step291.md`

## Tests Run

The main agent confirmed this issue after concurrent platform-interface work was
reconciled:

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
git diff --check
```

## Logs And Evidence

The solution build passed with 0 warnings and 0 errors.

The full host-test run emitted:

```text
TEST_PASS platform.firmware exitboot reports success
TEST_PASS platform.firmware exitboot reports stale map key
TEST_PASS platform.firmware exitboot rejects unavailable services
TEST_PASS host.tests
```

## Failures

No failures were observed in the final verification pass.

## Evidence Boundary

This step defines and host-tests the managed result boundary for
`ExitBootServices` planning. It does not prove a live firmware shutdown, memory-map
recapture after a real stale map key, `ExitBootServices` retry behavior in QEMU,
post-firmware serial/framebuffer operation, owned storage/network drivers, hosted
.NET 10, ASP.NET Minimal API, or Kestrel support.

This step does not make the OS complete, cloud-ready, production-ready, or ready
to run hosted .NET workloads.

## Deferred Work

- Wire the live EFI `ExitBootServices` function pointer through the platform
  firmware implementation.
- Capture the final memory map immediately before a real exit attempt.
- Retry the exit path after stale map-key rejection.
- Prove the real `ExitBootServices` transition in QEMU with serial evidence.
- Add UEFI-gone guards that reject forbidden firmware calls after the transition.
- Keep this boundary aligned with future allocator, ACPI snapshot, framebuffer
  snapshot, storage, and network driver initialization work.
