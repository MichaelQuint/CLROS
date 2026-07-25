# Step 153 - QEMU Serial Marker Summary Parser Contract

## What Changed

- Added `SerialMarkerSummary` as the QEMU runner-local pure text parser for serial summary behavior.
- Routed `OS.QemuRunner` polling and summary output through the parser for required markers, forbidden markers, missing markers, and ordered observed marker lines.
- Added focused host tests for the serial summary contract.
- Updated QEMU, libvirt, and completion-gate docs to record the parser boundary and evidence.

## Why It Changed

Gitea issue #175 needs a deterministic host-side parser contract for QEMU serial marker summaries. This keeps required-marker, forbidden-marker, missing-marker, and observed-marker ordering semantics testable without starting QEMU or touching kernel, driver, storage, networking, or std code.

## Files Changed

- `tools/OS.QemuRunner/SerialMarkerSummary.cs`
- `tools/OS.QemuRunner/Program.cs`
- `tests/OS.HostTests/SerialMarkerSummaryTests.cs`
- `tests/OS.HostTests/OS.HostTests.csproj`
- `docs/qemu.md`
- `docs/libvirt.md`
- `docs/completion-gates.md`
- `done/step153.md`

## Tests Run

```powershell
dotnet build .\tools\OS.QemuRunner\OS.QemuRunner.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build *> .\artifacts\logs\host-tests-issue175.log
```

## Logs And Evidence

- `dotnet build .\tools\OS.QemuRunner\OS.QemuRunner.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- Host-test log emitted:
  - `TEST_PASS qemu.serial summary passes required markers`
  - `TEST_PASS qemu.serial summary reports missing markers`
  - `TEST_PASS qemu.serial summary reports forbidden markers`
  - `TEST_PASS qemu.serial summary preserves marker order`
  - `TEST_PASS host.tests`

## Failures

- An early host-test build attempt failed because another `OS.HostTests` process temporarily locked `tests/OS.HostTests/bin/Debug/net10.0/OS.Kernel.dll`. The build passed after the lock cleared.
- An attempted alternate output build used a shared intermediate path and produced duplicate generated assembly attributes. Those generated `issue175` folders were removed, and the normal focused build path passed.
- A PowerShell 5 `Add-Type` parser smoke attempt could not load the .NET 10 runner assembly. The committed host tests provided the final parser evidence instead.

## Deferred Work

- No real QEMU process execution was added.
- No libvirt serial polling was implemented.
- No kernel, driver, storage, networking, or std behavior was changed.

## Evidence Boundary

This step is pure host text parsing evidence. It proves the QEMU runner can summarize serial text consistently for marker presence, marker absence, forbidden marker hits, and observed marker order. It does not prove firmware boot, `ExitBootServices`, owned drivers, storage, networking, libvirt boot, hosted .NET, Minimal API, or Kestrel.
