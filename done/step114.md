# Step 114 - Active TCP Connect Transaction

## What Changed

- Added `TcpActiveConnectTransaction`, a caller-driven mock TCP active-connect transaction for `CLOSED -> SYN_SENT -> ESTABLISHED`.
- Added host tests for successful active connect, bad SYN-ACK rejection, invalid-state/insufficient-buffer checks, and caller-driven timeout.
- Updated networking and completion-gate docs to record the new mock-only evidence boundary.

## Why

Gitea issue #132 needs an active TCP connect transaction that moves the network stack toward a future socket/PAL connect path without claiming live TCP, virtio-net, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.

## Files Changed

- `src/OS.Kernel/Net/TcpActiveConnectTransaction.cs`
- `tests/OS.HostTests/TcpActiveConnectTransactionTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step114.md`

## Tests Run

```text
dotnet build .\OS.slnx --nologo -m:1
Build succeeded. 0 Warning(s), 0 Error(s)

dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
TEST_PASS net.tcp active connect establishes
TEST_PASS net.tcp active connect rejects bad synack
TEST_PASS net.tcp active connect times out
TEST_PASS host.tests

.\scripts\check-naming.ps1
TEST_PASS naming.guard
TEST_PASS source.firewall

git diff --check -- src\OS.Kernel\Net\TcpActiveConnectTransaction.cs tests\OS.HostTests\TcpActiveConnectTransactionTests.cs docs\networking.md docs\completion-gates.md done\step114.md
Exited 0 with existing LF-to-CRLF warnings for docs files only.
```

## Failures

- None in the parent-owned verification run.

## Evidence Boundary

This is mock TCP segment-buffer evidence only. The transaction does not retransmit SYN segments, own timers, allocate TCP control blocks, validate pseudo-header checksums, transfer stream data, close connections, expose sockets/PAL exports, perform virtio-net packet I/O, host .NET 10, run ASP.NET Minimal API, or prove Kestrel behavior.

## Deferred Work

- Add a full TCP control block and connection table.
- Add retransmission timers and sequence-window policy.
- Wire active connect to future PAL socket connect behavior.
- Promote the transaction to guest/QEMU evidence only after virtio-net packet I/O exists.
