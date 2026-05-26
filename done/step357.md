# Step 357 - Virtio-net device config register-window fake read

## Summary

Added a host-tested virtio-net device-configuration register-window reader that
copies MAC/status bytes from caller-owned device BAR-window bytes into
caller-owned scratch storage, then parses the bytes through the existing
`VirtioNetDeviceConfig` parser.

## Why

The virtio-net path needs a checked bridge from discovered device-configuration
aperture metadata to the existing MAC/status parser before a future live BAR
accessor can read device config registers. This slice keeps the boundary fake
and caller-owned while preserving register-window, access-execution, scratch,
and parse failure statuses.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetDeviceConfig.cs`
- `tests/OS.HostTests/VirtioNetDeviceConfigTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `docs/drivers.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step357.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet\.config\.windowexec|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS drivers.virtionet.config.windowexec reads mac address
TEST_PASS drivers.virtionet.config.windowexec reads optional status
TEST_PASS drivers.virtionet.config.windowexec rejects short scratch buffer
TEST_PASS drivers.virtionet.config.windowexec propagates register window failure
TEST_PASS drivers.virtionet.config.windowexec propagates access failure
TEST_PASS drivers.virtionet.config.windowexec propagates config parse failure
TEST_PASS drivers.virtionet.config.windowexec preserves no-live-mmio boundary
TEST_PASS host.tests
```

Full solution build passed with 0 warnings and 0 errors. `git diff --check`
reported no whitespace errors; it only emitted existing LF-to-CRLF working-copy
warnings on the Windows host.

## Failures

None in the focused build, marker scan, solution build, or diff check.

## Deferred work

- Replace fake byte-span register-window reads with a live BAR-backed volatile
  accessor only after BAR mapping, MMIO ownership, and device-memory ordering are
  implemented and tested.
- Do not treat this as live virtio-net discovery, live packet I/O, cloud
  readiness, hosted .NET 10 readiness, ASP.NET Minimal API readiness, or Kestrel
  evidence.

## Evidence boundary

This proves only fake register-window reads over caller-owned device BAR-window
bytes and caller-owned scratch storage. It does not map BARs, read live device
registers, stabilize device-configuration generation, negotiate features,
configure queues, allocate or map DMA memory, notify a device, process
interrupts, transfer packets, or prove cloud/hypervisor readiness.
