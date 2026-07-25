# Step 290 - Platform UEFI-Gone Simulation Adapters

## What Changed

- Added guarded platform adapters for firmware console output and memory-map
  access.
- Extended the UEFI-gone simulation path to exercise guarded console,
  memory-map, and configuration-table adapters after the firmware mode is
  changed to `UefiGoneSimulated`.
- Added status-coded `PlatformUefiGoneSimulationResult` values for successful
  denial, simulation transition denial, console call leakage, memory-map call
  leakage, and configuration-table call leakage.
- Preserved stable markers including `UEFI_GONE_SIM_START`,
  `UEFI_GONE_SIM_PLATFORM_DENIED`, `UEFI_GONE_SIM_PASS`, and
  `UEFI_GONE_SIM_FAIL reason=<status>`.
- Added host tests proving the guarded adapters deny calls after simulated
  UEFI-gone mode and do not touch their backing sources.

## Why It Changed

Before a real `ExitBootServices` transition is enabled, the OS needs a cheap
way to prove firmware-backed adapters fail closed once firmware services are no
longer available. This step keeps the check host-testable and probe-driven
without changing the default live EFI marker order.

## Files Changed

- `src/OS.Kernel/Platform/GuardedPlatformConsole.cs`
- `src/OS.Kernel/Platform/GuardedPlatformMemoryMap.cs`
- `src/OS.Kernel/Platform/IPlatformConfigurationTables.cs`
- `src/OS.Kernel/Platform/PlatformUefiGoneSimulation.cs`
- `tests/OS.HostTests/PlatformUefiGoneSimulationTests.cs`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/pal.md`
- `docs/completion-gates.md`
- `done/step290.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

The solution build passed with 0 warnings and 0 errors.

The full host-test run emitted:

```text
TEST_PASS platform.uefi gone simulation denies console memorymap and config adapters
TEST_PASS platform.uefi gone simulation reports console allowance
TEST_PASS platform.uefi gone simulation rejects already-exited firmware
TEST_PASS platform.config tables scans ACPI before UEFI gone
TEST_PASS platform.config tables rejects scan in UEFI-gone simulation
TEST_PASS host.tests
```

## Failures

No failures were observed in the final verification pass.

## Evidence Boundary

This is host-test evidence for guarded platform adapters and a simulation
result contract. The live EFI simulation probe still emits its existing
`UEFI_GONE_SIM_PASS` marker, but this step does not route live `EfiEntry`
through these managed adapters, does not call `ExitBootServices`, and does not
prove post-firmware serial, framebuffer, storage, networking, hosted .NET 10,
ASP.NET Minimal API, or Kestrel support.

## Deferred Work

- Route additional platform adapters through the same simulation contract as
  filesystem, timer, keyboard, random, framebuffer, and storage boundaries are
  introduced.
- Add adapter-qualified serial markers for explicit simulation probe builds once
  the live EFI path can emit them without confusing normal QEMU smoke.
- Keep normal QEMU smoke forbidding `UEFI_GONE_SIM_*` markers unless the
  simulation probe is explicitly enabled.
- Require owned diagnostics before any future real `ExitBootServices` call.
