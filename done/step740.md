# Step 740 - FetchApp HTTP TCP Exchange Plan

## Summary
Added a host-tested FetchApp HTTP TCP exchange composer that drives an already-prepared FetchApp TCP context through caller-owned SYN, SYN-ACK, final ACK, HTTP GET request, HTTP response, completion evaluation, and active-close FIN/ACK frame buffers.

## Why
The networking stack had separate host-tested pieces for FetchApp endpoint derivation, DNS/ARP-to-TCP context composition, HTTP GET over TCP/IPv4 frames, FetchApp completion evaluation, and active close frame generation. This slice ties them into one value-only boundary so future FetchApp handoff work can depend on a single status-coded exchange contract without claiming live NIC or socket behavior.

## Files changed
- `src/OS.Kernel/Net/FetchAppHttpTcpExchangePlan.cs`
- `tests/OS.HostTests/FetchAppHttpTcpExchangePlanTests.cs`
- `docs/networking.md`
- `done/step740.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence
- Build log: `artifacts/logs/issue704-host-build-initial-20260525.log`
- Host-test log: `artifacts/logs/issue704-hosttests-initial-20260525.log`
- Expected markers:
  - `TEST_PASS fetch.http tcp exchange completes completion and close`
  - `TEST_PASS fetch.http tcp exchange rejects context failure`
  - `TEST_PASS fetch.http tcp exchange rejects short endpoint buffers`
  - `TEST_PASS fetch.http tcp exchange rejects bad synack checksum`
  - `TEST_PASS fetch.http tcp exchange rejects request frame too small`
  - `TEST_PASS fetch.http tcp exchange rejects missing expected marker`
  - `TEST_PASS fetch.http tcp exchange rejects close frame too small`
  - `TEST_PASS fetch.http tcp exchange default result is not success`
  - `TEST_PASS host.tests`

## Failures
- None in the final build or host-test run for this slice.

## Deferred work
- Live virtio-net RX/TX queue integration, BAR mapping, DMA-safe buffers, interrupts, timers, retransmission, TCP control-block ownership, socket/PAL export wiring, DNS/ARP live transaction execution, FetchApp process execution, TLS, hosted .NET 10, ASP.NET Minimal API, and Kestrel behavior remain pending.
- QEMU boot proof and cloud network proof remain outside this host-only slice.

## Evidence boundary
- Host-tested caller-owned frame composition only. The result proves deterministic sequencing and failure preservation across existing planner contracts; it does not prove live packet I/O, sockets, PAL calls, FetchApp execution, Kestrel support, or cloud readiness.

