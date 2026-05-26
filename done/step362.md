# Step 362 - Virtio-net RX register-window completion classification

## Summary

Added a host-tested RX register-window completion/classification composer that
consumes a successful RX register-window readiness result, validates one
caller-owned used-ring completion through the existing receive lifecycle, and
classifies the completed caller-owned receive buffer.

## Why

RX readiness already proved queue setup register-window fake execution,
caller-owned RX descriptor publication, explicit barrier metadata, and
barrier-gated notify register-window fake execution. The next narrow boundary
was to compose that readiness with the existing receive completion and
classifier path without claiming live hardware, interrupts, packet I/O, cloud
readiness, or Kestrel readiness.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetReceiveRegisterWindowCompletionPlan.cs`
- `tests/OS.HostTests/VirtioNetReceiveRegisterWindowCompletionPlanTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `docs/drivers.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step362.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet\.rx\.completion\.windowexec|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS drivers.virtionet.rx.completion.windowexec composes readiness completion classification
TEST_PASS drivers.virtionet.rx.completion.windowexec rejects failed readiness
TEST_PASS drivers.virtionet.rx.completion.windowexec rejects no completion
TEST_PASS drivers.virtionet.rx.completion.windowexec propagates classifier failure
TEST_PASS drivers.virtionet.rx.completion.windowexec preserves no-live-mmio boundary
TEST_PASS host.tests
```

The focused host build and full solution build passed with 0 warnings and 0
errors. `git diff --check` reported no whitespace errors; it only emitted
LF-to-CRLF working-copy warnings on the Windows host.

## Failures

None in the focused build, marker scan, full solution build, or diff check.

## Deferred work

- Replace fake register-window notification and caller-owned used-ring snapshots
  with live BAR-backed notification/completion evidence only after BAR mapping,
  MMIO ownership, DMA ownership, and real ordering primitives exist.
- Keep RX completion limited to one caller-owned used-ring entry until a later
  issue implements multi-completion draining, interrupt/poll integration, and
  runtime receive-buffer recycling.
- Do not treat this as live virtio-net packet I/O, networking readiness, cloud
  readiness, hosted .NET 10 readiness, ASP.NET Minimal API readiness, or Kestrel
  evidence.

## Evidence boundary

This proves only RX register-window completion/classification composition over
caller-owned queue memory and caller-owned receive buffers after successful RX
register-window readiness. It does not map BARs, perform volatile/live MMIO,
execute a real memory barrier, notify a live device, allocate or map DMA memory,
process interrupts, poll hardware, transfer packets, or prove networking, cloud,
hosted .NET 10, ASP.NET Minimal API, or Kestrel readiness.

