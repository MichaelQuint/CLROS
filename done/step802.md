# Step 802 - Virtio-net RX TCP dispatch composition

## Summary
Implemented a host-tested virtio-net register-window RX slab completion to TCP
receive-dispatch composition boundary.

## Why
Established TCP receive metadata previously had host coverage through
`TcpReceiveDispatcher` with synthetic network-dispatch inputs. This step proves
the same TCP receive path can be reached from the completed caller-owned
virtio-net RX slab boundary while preserving nested RX, slab, network, and TCP
metadata.

## Files changed
- `src/OS.Kernel/Drivers/VirtioNetReceiveRegisterWindowTcpDispatchPlan.cs`
- `tests/OS.HostTests/VirtioNetReceiveRegisterWindowTcpDispatchPlanTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step802.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:OutDir=.\artifacts\worker761-hosttests\`
- `dotnet .\tests\OS.HostTests\artifacts\worker761-hosttests\OS.HostTests.dll`

## Evidence
- Default host-test build passed with 0 warnings and 0 errors after the active
  storage worker's missing adapter landed.
- Default host-test run currently fails before reaching this slice because an
  unrelated QEMU runner test fails:
  `TEST_FAIL qemu dry-run emits native app marker requirements` and
  `TEST_FAIL qemu.runner.tests failures=1`.
- Isolated host-test build passed with 0 warnings and 0 errors.
- Isolated host-test run emitted:
  - `TEST_PASS drivers.virtionet.rx.tcp.dispatch.windowexec accepts established payload`
  - `TEST_PASS drivers.virtionet.rx.tcp.dispatch.windowexec acknowledges passive fin`
  - `TEST_PASS drivers.virtionet.rx.tcp.dispatch.windowexec rejects failed rx dispatch`
  - `TEST_PASS drivers.virtionet.rx.tcp.dispatch.windowexec rejects non-TCP protocol`
  - `TEST_PASS drivers.virtionet.rx.tcp.dispatch.windowexec rejects bad TCP checksum or TCP dispatch failure`
  - `TEST_PASS drivers.virtionet.rx.tcp.dispatch.windowexec rejects failed TCP stream path`
  - `TEST_PASS drivers.virtionet.rx.tcp.dispatch.windowexec default result is not success`
  - `TEST_PASS host.tests`

## Failures
- The required default host-test run is currently blocked by the unrelated QEMU
  runner test failure above in a dirty worktree with active worker overlap.

## Deferred work
- No live virtio-net RX, packet I/O, sockets, PAL socket receive, TCP control
  block ownership, retransmission, timers, hosted .NET, ASP.NET Minimal API, or
  Kestrel behavior is proven by this step.

## Evidence boundary
- Host-tested caller-owned queue memory, receive-buffer slab, selected-buffer
  slice, network-dispatch metadata, and TCP receive-dispatch metadata only.
  The slice performs no live MMIO, packet I/O, socket dispatch, or Kestrel
  startup.

