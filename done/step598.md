# Step 598 - PAL mock socket connect boundary

## Summary

Added host-tested outbound `Connect` behavior to the mock PAL socket table and
facade. The slice records remote endpoint metadata for an IPv4 TCP stream
socket only after the socket has a pre-bound local endpoint, makes the connected
handle usable by the existing mock send/drain path, and rejects invalid handles,
unbound sockets, listener sockets, invalid remote endpoints, and invalid facade
timeouts through deterministic `OSHostStatus` values.

## Why

The hosted .NET/Kestrel PAL ledger already declares `OSHost_SocketConnect`, but
the host-only mock table/facade covered only inbound listener and accepted-peer
state. This step narrows that gap without claiming live sockets: it gives
future outbound HTTP/DNS/client work a deterministic in-process contract for
connect request validation and endpoint metadata while preserving the
no-export/no-network boundary.

## Files changed

- `src/OS.Kernel/Pal/OSHostSocketTable.cs`
  - Added `Connect(OSHostSocketHandle, OSHostIpv4Endpoint)`.
  - Added slot transition from bound to connected state with remote endpoint
    metadata and connected mock buffers.
- `src/OS.Kernel/Pal/OSHostSocketFacade.cs`
  - Added `Connect(OSHostSocketConnectRequest)` request validation and
    status translation over the table.
- `tests/OS.HostTests/PalSocketTableTests.cs`
  - Added table tests for successful outbound metadata and invalid state
    rejection.
- `tests/OS.HostTests/PalSocketFacadeTests.cs`
  - Added facade test for connect request validation and remote endpoint query.
- `docs/pal.md`
  - Documented current connect behavior and evidence boundary.
- `work/PAL/pal-design.md`
  - Updated the PAL design notes to include host-only connect metadata.
- `docs/completion-gates.md`
  - Updated hosted-tier/PAL evidence rows and markers without changing the
    failing Kestrel gate status.

## Tests run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'TEST_FAIL|Unhandled|Exception|pal\.socket connect|pal\.socket facade connects|host\.tests'
```

## Evidence

- Host test build succeeded with 0 warnings and 0 errors.
- Focused host harness output included:
  - `TEST_PASS pal.socket facade connects with remote endpoint`
  - `TEST_PASS pal.socket connect establishes outbound endpoint`
  - `TEST_PASS pal.socket connect rejects invalid state`
  - `TEST_PASS host.tests`

## Failures

- None in the focused verification.

## Deferred work

- No unmanaged `OSHost_SocketConnect` export exists.
- No DNS, routing, TCP handshake, packet I/O, scheduler wait, timeout wait, or
  live network device behavior is implemented by this mock.
- Kestrel remains blocked on exported PAL functions, scheduler/threading,
  package/config integration, live networking, and hosted .NET launch evidence.

