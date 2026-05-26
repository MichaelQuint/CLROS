# Step 742 - FetchApp HTTP TCP Exchange Diagnostic Formatter

## Summary

Added a caller-owned diagnostic formatter for `FetchAppHttpTcpExchangePlan`
results. Successful exchange results now have a stable
`FETCH_HTTP_TCP_EXCHANGE` marker that records the nested context, TCP GET,
completion, close, frame-byte, and final sequence-number metadata needed by
future serial/QEMU probes without dumping host, path, or expected marker bytes.

## Why

The exchange composer from step 740 created one value-only boundary for the
FetchApp HTTP TCP path, but future boot and QEMU evidence needs a parseable
serial marker that can be emitted from already-produced exchange metadata. This
slice adds that marker formatting contract without adding sockets, PAL calls, or
live virtio-net I/O.

## Files changed

- `src/OS.Kernel/Net/FetchAppHttpTcpExchangePlanDiagnosticFormatter.cs`
  - New status/result types and formatter/query helpers.
  - Formats `FETCH_HTTP_TCP_EXCHANGE` into caller-owned spans.
  - Rejects default/uninitialized exchange results and preserves short
    destination required/available byte counts without partial writes.
- `tests/OS.HostTests/FetchAppHttpTcpExchangePlanTests.cs`
  - Added success-marker, short-destination, and default-result rejection tests.
- `docs/networking.md`
  - Documented the formatter, marker fields, and host-only evidence boundary.
- `done/step742.md`
  - Recorded this implementation slice and evidence.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue706-fetch-http-tcp-exchange-formatter\bin\Debug\net10.0`
- `dotnet .\artifacts\issue706-fetch-http-tcp-exchange-formatter\bin\Debug\net10.0\OS.HostTests.dll`

## Evidence

- `artifacts/logs/issue706-host-build-initial-20260525.log`
  - The default host-test build was blocked by a locked
    `OS.HostTests.exe` output file from another running harness process.
- `artifacts/logs/issue706-host-build-shaped-20260525.log`
  - Shaped host-test build succeeded with 0 warnings and 0 errors.
- `artifacts/logs/issue706-hosttests-shaped-20260525.log`
  - Full shaped host harness ended with `TEST_PASS host.tests`.
  - New markers include:
    - `TEST_PASS fetch.http tcp exchange formatter formats success marker`
    - `TEST_PASS fetch.http tcp exchange formatter reports short destination`
    - `TEST_PASS fetch.http tcp exchange formatter rejects default result`

## Failures

No final verification failures for the shaped build/test path.

The initial default build attempt failed because an existing host-test process
held the default output executable. The shaped `UseAppHost=false` build avoided
that shared output path and completed successfully.

## Deferred work

- Wire the formatter into a future boot/QEMU probe only after the default boot
  path has live or staged FetchApp exchange metadata to report.
- Live virtio-net RX/TX queue integration, BAR mapping, DMA-safe buffers,
  interrupts, timers, retransmission, TCP control-block ownership,
  socket/PAL export wiring, DNS/ARP live transaction execution, FetchApp
  process execution, TLS, hosted .NET 10, ASP.NET Minimal API, and Kestrel
  behavior remain pending.
- QEMU boot proof and cloud network proof remain outside this host-only slice.

## Evidence boundary

Host-tested diagnostic formatting only. The result proves stable serialization
of existing caller-owned exchange metadata; it does not prove live packet I/O,
sockets, PAL calls, FetchApp execution, Kestrel support, or cloud readiness.
