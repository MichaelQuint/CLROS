# Step 160 - Virtio PCI Capability List Parser Contract

## What Changed

- Added `VirtioPciCapabilityListParser` as a pure parser for standard PCI capability headers from caller-provided configuration-space bytes.
- Added status-coded decoding for modern virtio vendor-specific capability records with capability type, BAR, BAR-relative offset, BAR-relative length, and notify offset multiplier metadata.
- Added detection for capability-list loops, unaligned offsets, truncated headers, out-of-range vendor records, unsupported virtio vendor records, invalid BARs, overflowing BAR-relative regions, and missing required virtio common/notify/ISR/device configuration capabilities.
- Added host tests proving required capability capture, malformed-list rejection, loop rejection, and handoff into `VirtioPciDeviceDiscovery`.

## Why It Changed

Issue #179 needs a deterministic boundary between future PCI ECAM config-space snapshots and the existing virtio PCI discovery contract. This slice keeps parsing separate from live PCI access: it does not read config space, map BARs, negotiate features, create virtqueues, or perform block/network I/O.

## Files Changed

- `src/OS.Kernel/Drivers/VirtioPciCapabilityListParser.cs`
- `tests/OS.HostTests/VirtioPciCapabilityListParserTests.cs`
- `docs/drivers.md`
- `docs/cstorage.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step160.md`

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build *> .\artifacts\logs\host-tests-issue179-fixed.log
```

## Logs And Evidence

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1` passed with 0 warnings and 0 errors after an earlier retry was blocked by another running `OS.HostTests.exe`.
- `artifacts/logs/host-tests-issue179-fixed.log` exited with `HOST_TEST_EXIT 0` and emitted:
  - `TEST_PASS drivers.virtiopci.capability parser records required capabilities`
  - `TEST_PASS drivers.virtiopci.capability parser rejects truncated capability`
  - `TEST_PASS drivers.virtiopci.capability parser rejects capability loop`
  - `TEST_PASS drivers.virtiopci.capability parser feeds discovery contract`
  - `TEST_PASS host.tests`

## Failures

- The first integrated run exposed a test setup bug: the truncated-capability test wrote past its intentionally short buffer before the parser could reject it. The test now writes a valid backing buffer and passes a truncated span to the parser.
- A later host-test build attempt failed because another concurrent `OS.HostTests.exe` process locked the default test apphost. Retrying after the process exited passed.
- An isolated-output build experiment failed because tool project generated `obj` artifacts outside the default intermediate path were pulled into `OS.QemuRunner`; this was not used as final evidence.

## Deferred Work

- No live PCI ECAM scanning is implemented.
- No PCI config-space reads or writes are performed.
- No BAR mapping or BAR aperture validation is implemented.
- No virtio feature negotiation, queue setup, interrupts, block reads, or network packet transfer is implemented.

## Evidence Boundary

This step is host-tested byte parsing and metadata handoff only. It proves a future PCI enumerator can feed a config-space snapshot into a deterministic virtio capability parser and then into `VirtioPciDeviceDiscovery`. It does not prove device enumeration, hardware ownership, storage, networking, or Kestrel readiness.
